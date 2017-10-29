namespace LiveRoku.Core {
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using LiveRoku.Base;
    using LiveRoku.Base.Logger;
    [SuppressMessage ("Microsoft.Performance", "CS4014")]
    public class LiveFetchController : LiveEventEmitterBase, ILiveFetcher, IDisposable {
        public ISettingsBase Extra => extra;
        public bool IsRunning { get; private set; }//Value of if it's start download
        public bool IsStreaming => downloader.IsStreaming;//Result of it's downloading or not
        public bool IsLiveOn => dmCarrier.IsLiveOn;//The live status

        private readonly ISettings extra = new EasySettings ();
        private readonly CancellationManager cancelMgr = new CancellationManager ();
        private readonly INetworkWatcher network = new NetworkWatcherProxy ();
        private readonly BiliApi accessApi; //API access
        private readonly IFetchArgsHost settings; //Provide base parameters
        private readonly FetchArgsBean argsTemp;
        private readonly DanmakuCarrier dmCarrier;
        private readonly LiveDownloadWorker downloader;
        private readonly int requestTimeout;

        public LiveFetchController (IFetchArgsHost settings, int requestTimeout) {
            var userAgent = settings?.UserAgent;
            //Initialize access api
            this.accessApi = new BiliApi (makeNewWebClient, Logger, userAgent);
            this.argsTemp = new FetchArgsBean(-1, accessApi, Logger);
            this.requestTimeout = requestTimeout;
            this.settings = settings;
            //.............
            //Make chat/download worker
            //And subscribe base events for boardcasting
            //.............
            dmCarrier = new DanmakuCarrier(this, accessApi) {
                HotUpdated = base.onHotUpdate,
                DanmakuRecv = dm => {
                    this.downloader.danmakuToLocal(dm);
                    base.danmakuRecv(dm);
                },
                LiveStatusUpdated = isOn => {
                    this.confirmDownloadWhenLiveTurn(isOn);
                    base.onStatusUpdate(isOn);
                }
            };
            downloader = new LiveDownloadWorker(this, userAgent) {
                BitRateUpdated = bitRate => onBitRateUpdate(bitRate, $"{bitRate / 1000} Kbps"),
                DurationUpdated = duration => onDurationUpdate(duration, SharedHelper.getFriendlyTime(duration)),
                DownloadSizeUpdated = totalBytes => onDownloadSizeUpdate(totalBytes, totalBytes.ToFileSize()),
                MissionCompleted = mission => {
                    base.onMissionComplete(mission);
                    if (IsRunning) {//Back to waiting status
                        base.onWaiting();
                    }
                },
                OnStreaming = () => {
                    this.confirmChatCenter();
                    base.onStreaming();
                }
            };
        }
        public LiveFetchController (IFetchArgsHost argsHost):
            this (argsHost, 5000) { }

        public void Dispose () {
            stop ();
            dmCarrier.purgeEvents ();
            emptyHandlers ();
        }

        public IRoomInfo getRoomInfo (bool refresh) {
            if (!IsRunning || refresh) {
                if (!int.TryParse (settings.RoomId, out int roomId)) {
                    return null;
                }
                if (argsTemp.OriginRoomId != roomId) {
                    argsTemp.resetOriginId (roomId);
                }
                argsTemp.fetchRoomInfo ();
            }
            return argsTemp.RoomInfo;
        }

        //.............
        //Start or Stop
        //.............
        public void stop (bool force = false) => cancelFetch (force, false);

        public void start () {
            if (IsRunning) return;
            //Basic initialize
            network.assumeAvailability (true);
            network.attach (available => {
                Logger.log (Level.Info, $"Network Availability ->  {available}");
                if (!available) cancelFetch (false, true);
                else if (!IsRunning) start ();
            });
            IsRunning = true;
            downloader.reset ();
            dmCarrier.resetState ();
            //Preparing signal
            this.onPreparing ();
            //Basic parameters check
            //Get running parameters
            var roomIdText = settings.RoomId;
            var folder = settings.Folder;
            var fileNameFormat = settings.FileFormat;
            if (!int.TryParse (roomIdText, out int roomId) || roomId <= 0) {
                Logger.log (Level.Error, "Wrong id.");
                stop ();
                return;
            }
            fetchLiveBy (roomId, folder, fileNameFormat, !extra.get("cancel-flv", false));
        }

        private void cancelFetch (bool force, bool callByInternal) {
            Debug.WriteLine ("stopImpl invoke");
            if (!callByInternal) { //Network watcher needless
                network.detach (); //detach now
            }
            cancelMgr.cancelAll ();
            cancelMgr.clear ();
            if (IsRunning) {
                IsRunning = false;
                dmCarrier.disconnect ();
                downloader.stop (force);
                Logger.log (Level.Info, "Downloader stopped.");
                this.onStopped ();
            }
        }

        private void fetchLiveBy (int roomId, string folder, string fileNameFormat, bool videoRequire) {
            //Prepare to start task
            //Cancel when no result back over five second
            var startCtl = new CancellationTokenSource ();
            cancelMgr.cancel ("fetchUrl-start-impl");
            //Prepare real roomId and flv url
            bool isUpdated = false;
            runOnlyOne (() => {
                argsTemp.resetOriginId (roomId);
                isUpdated = argsTemp.fetchUrlAndRealId ();
            }, "fetch-url-realId", requestTimeout).ContinueWith (task => {
                cancelMgr.set ("fetch-start-impl", startCtl);
                task.Exception?.printOn (Logger);
                //Check if get it successful
                if (isUpdated) {
                    //complete model
                    argsTemp.Folder = folder;
                    argsTemp.FileNameFormat = fileNameFormat;
                    argsTemp.AutoStart = settings.AutoStart;
                    argsTemp.VideoRequire = videoRequire;
                    argsTemp.DanmakuRequire = settings.DownloadDanmaku;
                    runOnlyOne (() => {
                        argsTemp.fetchRoomInfo ();
                    }, "fetch-room-info");
                    //All ready, start now
                    Logger.log (Level.Info, $"All ready, fetch: {argsTemp.FlvAddress}");
                    //All parameters ready
                    base.onWaiting ();
                    downloadThrough(downloader, argsTemp);
                    dmCarrier.connect (argsTemp.RealRoomId);
                } else {
                    Logger.log (Level.Error, $"Get room detail fail, RealRoomId : {argsTemp.RealRoomId}");
                    stop ();
                }
                cancelMgr.remove ("fetch-start-impl");
            }, startCtl.Token);
        }

        //.......
        //Handlers
        //.......
        private IWebClient makeNewWebClient() {
            return new StandardWebClient {
                Encoding = System.Text.Encoding.UTF8
            };
        }

        private void downloadThrough(LiveDownloadWorker downloader, FetchArgsBean @params) {
            var fileName = getFileFullName (@params.FileNameFormat, @params.Folder, @params.RealRoomId, DateTime.Now);
            if (@params.VideoRequire) {
                downloader.download(@params.FlvAddress, fileName, @params.DanmakuRequire);
            } else Logger.log(Level.Info, $"No video mode");
        }

        private void confirmChatCenter () {
            runOnlyOne (() => {
                if (dmCarrier.IsChannelActive) return;
                dmCarrier.connect (argsTemp.RealRoomId);
            }, nameof (confirmChatCenter));
        }

        private void confirmDownloadWhenLiveTurn (bool isOn) {
            if (!argsTemp.VideoRequire) return;
            if (!isOn) {
                cancelMgr.cancel (nameof (confirmDownloadWhenLiveTurn));
                cancelMgr.remove (nameof (confirmDownloadWhenLiveTurn));
                downloader.stop (false);
            } else if (argsTemp.AutoStart && !downloader.IsStreaming) {
                runOnlyOne (() => {
                    var isUpdated = argsTemp.fetchUrlAndRealId ();
                    Logger.log (Level.Info, $"Flv address updated : {argsTemp.FlvAddress}");
                    if (isUpdated && IsRunning && dmCarrier.IsLiveOn && !downloader.IsStreaming) {
                        downloadThrough(downloader, argsTemp);
                    }
                }, nameof (confirmDownloadWhenLiveTurn), requestTimeout).ContinueWith (task => {
                    task.Exception?.printOn (Logger);
                }, TaskContinuationOptions.OnlyOnFaulted);
            }
        }

        //...........
        //Help method
        //...........
        private string getFileFullName (string format, string folder, int realRoomId, DateTime baseTime) {
            var fileName = string.Empty;
            try {
                fileName = format.Replace ("{roomId}", realRoomId.ToString ())
                    .Replace ("{Y}", baseTime.Year.ToString ("D4"))
                    .Replace ("{M}", baseTime.Month.ToString ("D2"))
                    .Replace ("{d}", baseTime.Day.ToString ("D2"))
                    .Replace ("{H}", baseTime.Hour.ToString ("D2"))
                    .Replace ("{m}", baseTime.Minute.ToString ("D2"))
                    .Replace ("{s}", baseTime.Second.ToString ("D2"));
            } catch (Exception e) {
                e.printStackTrace();
                fileName = $"{realRoomId}-{baseTime.ToString("yyyy-MM-dd-HH-mm-ss")}";
            }
            return Path.Combine (folder, fileName);
        }

        private Task runOnlyOne (Action action, string tokenKey, int timeout = 0) {
            var cts = timeout > 0 ? new CancellationTokenSource () :
                new CancellationTokenSource (timeout);
            cancelMgr.cancel (tokenKey);
            cancelMgr.set (tokenKey, cts);
            return Task.Run (action).ContinueWith (task => {
                cancelMgr.remove (tokenKey);
                task.Exception?.printOn (Logger);
            }, cts.Token);
        }

        private bool isValueTrue (IDictionary<string, object> dict, string key) {
            return dict.ContainsKey (key) && dict[key] is bool && ((bool) dict[key]);
        }

        //var dict = new Dictionary<T1, T2> { { key, value } };
    }

}