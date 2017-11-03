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
    using LiveRoku.Core.Common;
    using LiveRoku.Core.Models;
    [SuppressMessage ("Microsoft.Performance", "CS4014")]
    public class LiveFetchController : LiveEventEmitterBase, ILiveFetcher, IDisposable {
        public ISettingsBase Extra => extra;
        public bool IsRunning { get; private set; }//Value of if it's start download
        public bool IsStreaming => downloader.IsStreaming;//Result of it's downloading or not
        public bool? IsLiveOn => dmCarrier.IsLiveOn;//The live status

        private readonly ISettings extra = new EasySettings ();
        private readonly CancellationManager cancelMgr = new CancellationManager ();
        private readonly INetworkWatcher network = new NetworkWatcherProxy ();
        private readonly BiliApi accessApi; //API access
        private readonly IFetchArgsHost basicArgs; //Provide base parameters
        private readonly FetchArgsBean settings;
        private readonly DanmakuCarrier dmCarrier;
        private readonly LiveDownloadWorker downloader;
        private readonly int requestTimeout;
        private readonly object locker = new object();

        public LiveFetchController (IFetchArgsHost basicArgs, int requestTimeout) {
            var userAgent = basicArgs?.UserAgent;
            //Initialize access api
            this.accessApi = new BiliApi (makeNewWebClient, Logger, userAgent);
            this.settings = new FetchArgsBean(accessApi, Logger);
            this.requestTimeout = requestTimeout;
            this.basicArgs = basicArgs;
            //.............
            //Make chat/download worker
            //And subscribe base events for boardcasting
            //.............
            dmCarrier = new DanmakuCarrier(this, accessApi) {
                HotUpdated = base.onHotUpdate,
                LiveStatusUpdated = base.onStatusUpdate,
                LiveCommandRecv = confirmDownloadForLiveCommand,
                DanmakuRecv = dm => {
                    this.downloader.danmakuToLocal(dm);
                    base.danmakuRecv(dm);
                },
            };
            downloader = new LiveDownloadWorker(this, userAgent) {
                BitRateUpdated = bitRate => onBitRateUpdate(bitRate, $"{bitRate / 1000} Kbps"),
                DurationUpdated = duration => onDurationUpdate(duration, SharedHelper.getFriendlyTime(duration)),
                DownloadSizeUpdated = totalBytes => onDownloadSizeUpdate(totalBytes, totalBytes.ToFileSize()),
                MissionCompleted = mission => {
                    base.onMissionComplete(mission);
                    //Back to waiting status
                    if (IsRunning) base.onWaiting();
                },
                OnStreaming = () => {
                    this.confirmChatCenter();
                    base.onStreaming();
                }
            };
        }
        public LiveFetchController (IFetchArgsHost basicArgs):
            this (basicArgs, 5000) { }

        public void Dispose () {
            stop ();
            dmCarrier.purgeEvents ();
            emptyHandlers ();
        }

        public IRoomInfo getRoomInfo (bool refresh) {
            if (!IsRunning || refresh) {
                string originId = basicArgs.ShortRoomId;
                if (int.TryParse (originId, out int roomId)) {
                    if (!originId.Equals(settings.ShortRoomId)) {
                        settings.resetOriginId(originId);
                    }
                    var info = settings.fetchRoomInfo();
                    downloader.addRoomInfo(info);
                }
            }
            return settings.RoomInfo;
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
            //Basic parameters check
            //Get running parameters
            var roomIdText = basicArgs.ShortRoomId;
            if (!int.TryParse (roomIdText, out int roomId) || roomId <= 0) {
                Logger.log (Level.Error, "Wrong id.");
                stop ();
            } else {
                //Copy parameters
                settings.resetOriginId(roomIdText);
                settings.Folder = basicArgs.Folder;
                settings.FileNameFormat = basicArgs.FileFormat;
                settings.AutoStart = basicArgs.AutoStart;
                settings.VideoRequire = basicArgs.VideoRequire;
                settings.DanmakuRequire = basicArgs.DanmakuRequire;
                settings.IsShortIdTheRealId = basicArgs.IsShortIdTheRealId;
                //Preparing signal
                this.Extra.put("video-require", settings.VideoRequire);
                this.onPreparing();
                fetchLiveImpl();
            }
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

        private void fetchLiveImpl () {
            //Prepare to start task
            //Cancel when no result back over five second
            //Prepare real roomId and flv url
            bool isUpdated = false;
            runOnlyOne (() => {
                isUpdated = settings.fetchUrlAndRealId ();
            }, "fetch-url-realId", requestTimeout).ContinueWith (task => {
                runOnlyOne(() => {
                    //Check if get it successful
                    if (isUpdated) {
                        runOnlyOne(() => {
                            var info = settings.fetchRoomInfo();
                            downloader.addRoomInfo(info);
                        }, "fetch-room-info");
                        //All ready, start now
                        Logger.log(Level.Info, $"All ready, fetch: {settings.FlvAddress}");
                        //All parameters ready
                        base.onWaiting();
                        downloadThrough(downloader, settings);
                        dmCarrier.connect(settings.RealRoomId);
                    } else {
                        Logger.log(Level.Error, $"Get room detail fail, RealRoomId : {settings.RealRoomId}");
                        stop();
                    }
                }, "fetch-start-impl");
            });
        }

        private Task downloadThrough(LiveDownloadWorker downloader, FetchArgsBean args) {
            var fileName = getFileFullName(args.FileNameFormat, args.Folder, args.RealRoomId, DateTime.Now);
            if (args.VideoRequire) {
                lock (downloader) {
                    return downloader.download(args.FlvAddress, fileName, args.DanmakuRequire);
                }
            } else {
                Logger.log(Level.Info, $"No video mode");
                return Task.FromResult(false);
            }
        }

        //.......
        //Handlers
        //.......
        private IWebClient makeNewWebClient() {
            return new StandardWebClient {
                Encoding = System.Text.Encoding.UTF8,
                RequestTimeout = requestTimeout * 3
            };
        }

        private void confirmChatCenter () {
            runOnlyOne (() => {
                if (dmCarrier.IsChannelActive) return;
                dmCarrier.connect (settings.RealRoomId);
            }, nameof (confirmChatCenter));
        }

        private bool isRestartDownloadAllow() {
            return IsRunning && dmCarrier.IsLiveOn == true && !downloader.IsStreaming;
        }

        private CompleteFirstInvoker invoker = null;
        private void confirmDownloadForLiveCommand (MsgTypeEnum type) {
            if (!settings.VideoRequire) return;
            if (type == MsgTypeEnum.LiveEnd) {
                invoker?.revoke();
                cancelMgr.cancel (nameof (confirmDownloadForLiveCommand));
                cancelMgr.remove (nameof (confirmDownloadForLiveCommand));
                Logger.log(Level.Info, "Trying to stop downloader.");
                downloader.stop (false);
            } else if (settings.AutoStart) {
                //enterTimes = 0;//Just need to know has newest request
                //Keep one enter, sometimes LiveStart msg will send over one time
                invoker = invoker ?? new CompleteFirstInvoker(() => {
                    runOnlyOne(tryToRestartDownload, nameof(confirmDownloadForLiveCommand), requestTimeout * 2, () => {
                        //Stop when timeout if download not start
                        if (!downloader.IsStreaming) {
                            downloader.stop(true);
                        }
                    }).ContinueWith(task => {
                        invoker.fireActionOk();
                    });
                });
                invoker.invoke();
            }
        }
        
        private void tryToRestartDownload(CancellationToken token) {
            if (!isRestartDownloadAllow() || !settings.fetchUrlAndRealId())
                return;
            token.ThrowIfCancellationRequested();
            Logger.log(Level.Info, $"Flv address updated : {settings.FlvAddress}");
            //Reconfirm download required and not start 
            if (isRestartDownloadAllow()) {
                downloadThrough(downloader, settings).Wait();
            }
        }

        //...........
        //Help method
        //...........
        private string getFileFullName (string format, string folder, string realRoomId, DateTime baseTime) {
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

        private Task runOnlyOne(Action<CancellationToken> action, string tokenKey, int timeout = 0, Action onCancelled = null) {
            var cts = timeout > 0 ? new CancellationTokenSource() :
                new CancellationTokenSource(timeout);
            if (onCancelled != null)
                cts.Token.Register(onCancelled);
            cancelMgr.cancel(tokenKey);
            cancelMgr.set(tokenKey, cts);
            return Task.Run(()=>action?.Invoke(cts.Token)).ContinueWith(task => {
                cancelMgr.remove(tokenKey);
                task.Exception?.printOn(Logger);
            }, cts.Token);
        }

        private Task runOnlyOne (Action action, string tokenKey, int timeout = 0, Action onCancelled = null) {
            var cts = timeout > 0 ? new CancellationTokenSource () :
                new CancellationTokenSource (timeout);
            if (onCancelled != null)
                cts.Token.Register(onCancelled);
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