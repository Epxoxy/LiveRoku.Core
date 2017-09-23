using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LiveRoku.Base;
using System.Net.NetworkInformation;
using System.Collections.Generic;
using System.Diagnostics;

namespace LiveRoku.Core {

    //Danmaku provider
    public interface IDanmakuSource {
        LowList<DanmakuResolver> DanmakuResolvers { get; }
    }

    public interface INetwordkWatcher {
        bool IsEnabled { get; }
        bool IsAvailable { get; }
        void assumeAvailabily(bool available);
        void attach(Action<bool> onNewNetworkAvailability);
        void detach();
        bool checkCanConnect(string hostNameOrAddress);
    }

    class NetworkWatcherProxy : INetwordkWatcher  {
        public bool IsEnabled { get; private set; }
        public bool IsAvailable { get; private set; }

        private Action<bool> onNewNetworkAvailability;

        public bool checkCanConnect(string hostNameOrAddress) {
            try {
                System.Net.Dns.GetHostEntry(hostNameOrAddress);
                return true;
            } catch {//Exception message is not a important part here
                return false;
            }
        }

        public void assumeAvailabily(bool available) {
            this.IsAvailable = available;
        }

        public void attach(Action<bool> onNewNetworkAvailability) {
            setWatchOrNot(true);
            this.onNewNetworkAvailability = onNewNetworkAvailability;
        }

        public void detach() {
            setWatchOrNot(false);
            this.onNewNetworkAvailability = null;
        }

        private void setWatchOrNot(bool watchIt) {
            IsEnabled = watchIt;
            NetworkChange.NetworkAvailabilityChanged -= proxyEvent;
            if (!watchIt) return;
            NetworkChange.NetworkAvailabilityChanged += proxyEvent;
        }

        private void proxyEvent(object sender, NetworkAvailabilityEventArgs e) {
            IsAvailable = e.IsAvailable;
            onNewNetworkAvailability?.Invoke(e.IsAvailable);
        }
    }

    public class LiveDownloader : ILiveDownloader, ILogger, IDanmakuSource, IDisposable {
        public LowList<ILiveDataResolver> LiveDataResolvers { get; private set; }
        public LowList<IStatusBinder> StatusBinders { get; private set; }
        public LowList<DanmakuResolver> DanmakuResolvers { get; private set; }
        public LowList<ILogger> Loggers { get; private set; }
        public bool IsRunning { get; private set; }
        public bool IsStreaming { get; private set; }
        public long RecordSize { get; private set; }
        public bool IsLiveOn => isLiveOn;

        private readonly Dictionary<string, object> extras = new Dictionary<string, object>();
        private readonly INetwordkWatcher network = new NetworkWatcherProxy();
        private readonly CancellationManager cancelMgr;
        private readonly BiliApi biliApi; //API access
        private readonly FlvDownloader flvFetcher; //Download flv video
        private readonly DanmakuClient danmakuClient; //Download danmaku
        private readonly EventSubmitHandler danmakuEvents;
        private DanmakuStorage danmakuStorage;
        private readonly IRequestModel model; //Provide base parameters
        private VideoInfo videoInfo;
        private int requestTimeout;
        private FetchBean settings;
        private bool isLiveOn;
        private long delayReconnectMs = 10; 

        public LiveDownloader (IRequestModel model, string userAgent, int requestTimeout) {
            cancelMgr = new CancellationManager ();
            //Initialize Downloaders
            this.danmakuClient = new DanmakuClient ();
            this.danmakuEvents = danmakuClient.Events;
            this.flvFetcher = new FlvDownloader (userAgent, null);
            //Initialize Handlers
            this.StatusBinders = new LowList<IStatusBinder> ();
            this.DanmakuResolvers = new LowList<DanmakuResolver> ();
            this.LiveDataResolvers = new LowList<ILiveDataResolver> ();
            this.Loggers = new LowList<ILogger> ();
            //Initialize Others
            this.biliApi = new BiliApi (this, userAgent);
            this.requestTimeout = requestTimeout;
            this.model = model;
            //Subscribe events
            this.danmakuEvents.OnLog = appendErrorMsg;
            this.danmakuEvents.DanmakuReceived = onDanmaku;
            this.danmakuEvents.HotUpdated = hotUpdated;
            this.flvFetcher.VideoInfoChecked = videoChecked;
            this.flvFetcher.IsRunningUpdated = downloadStatusUpdated;
            this.flvFetcher.BytesReceived += downloadSizeUpdated;
        }
        public LiveDownloader (IRequestModel model, string userAgent):
            this (model, userAgent, 5000) { }

        private void purgeEvents () {
            flvFetcher.BytesReceived -= onStreaming;
            flvFetcher.BytesReceived -= downloadSizeUpdated;
            flvFetcher.VideoInfoChecked = null;
            flvFetcher.IsRunningUpdated = null;
            danmakuEvents.purgeEvents ();
        }

        public void Dispose () {
            stop ();
            purgeEvents ();
            StatusBinders?.clear ();
            DanmakuResolvers?.clear ();
            LiveDataResolvers?.clear ();
        }
        
        public RoomInfo fetchRoomInfo(bool refresh) {
            if (!IsRunning || refresh) {
                if (settings == null) {
                    int roomId;
                    if (!int.TryParse(model.RoomId, out roomId)) return null;
                    settings = new FetchBean(roomId, biliApi);
                    settings.refreshAllAsync().Wait();
                }
                settings.fetchRoomInfoAsync().Wait();
            }
            return settings.RoomInfo;
        }

        //For extension
        public object getExtra(string key) {
            if (extras.ContainsKey(key)) return extras[key];
            return null;
        }

        //For extension
        public void setExtra(string key, object value) {
            if (extras.ContainsKey(key)) {
                extras[key] = value;
            } else {
                extras.Add(key, value);
            }
        }


        public void stop(bool force = false) => stopImpl(force, false);

        public void start (string ingore = null) {
            if (IsRunning) return;
            //Basic initialize
            network.assumeAvailabily(true);
            network.attach(onNetworkChanged);
            IsRunning = true;
            IsStreaming = false;
            RecordSize = 0;
            delayReconnectMs = 10;
            videoInfo = new VideoInfo ();
            updateLiveStatus(false, false);
            //Preparing signal
            forEachByTaskWithDebug (StatusBinders, binder => {
                binder.onPreparing ();
            });
            //Basic parameters check
            //Get running parameters
            var roomIdText = model.RoomId;
            var folder = model.Folder;
            int roomId;
            if (!int.TryParse (roomIdText, out roomId)) {
                appendErrorMsg ("Wrong id.");
                stop ();
                return;
            }
            startImpl(roomId, folder);
        }
        
        //Internal Impl
        private void stopImpl(bool force, bool internalCall) {
            if (!internalCall) {//Network watcher needless
                network.detach();//detach now
            }
            if (IsRunning) {
                IsRunning = false;
                IsStreaming = false;
                danmakuEvents.Closed = null;
                flvFetcher.stop ();
                danmakuClient.stop ();
                danmakuStorage?.stop(force);
                forEachByTaskWithDebug (StatusBinders, binder => {
                    binder.onStopped ();
                });
            }
        }

        //Internal Impl
        private void startImpl(int roomId, string folder) {
            //Prepare to start task
            //Cancel when no result back over five second
            var cts = new CancellationTokenSource (requestTimeout);
            //Prepare real roomId and flv url
            bool isUpdated = false;
            Task.Run (async () => {
                settings = new FetchBean (roomId, biliApi);
                settings.Logger = this;
                isUpdated = await settings.refreshAllAsync ();
            }, cts.Token).ContinueWith (task => {
                printException(task.Exception);
                //Check if get it successful
                if (isUpdated) {
                    settings.fetchRoomInfoAsync();
                    var fileName = Path.Combine (folder, model.formatFileName (settings.RealRoomIdText));
                    //complete model
                    settings.Folder = folder;
                    settings.FileFullName = fileName;
                    settings.AutoStart = model.AutoStart;
                    settings.DanmakuNeed = model.DownloadDanmaku;
                    //Create FlvDloader and subscribe event handlers
                    flvFetcher.updateSavePath (settings.FileFullName);
                    flvFetcher.BytesReceived -= onStreaming;
                    flvFetcher.BytesReceived += onStreaming;
                    danmakuEvents.Closed = reconnectOnError;
                    //All parameters ready
                    forEachByTaskWithDebug (StatusBinders, binder => {
                        binder.onWaiting ();
                    });
                    appendInfoMsg ($"All ready, fetch: {settings.FlvAddress}");
                    //All ready, start now
                    flvFetcher.start (settings.FlvAddress);
                    Task.Run(() => {
                        tryConnect(danmakuClient, biliApi, settings.RealRoomId);
                    });
                } else {
                    appendErrorMsg ($"Get value fail, RealRoomId : {settings.RealRoomIdText}");
                    stop ();
                }
            });
        }
        


        //Stop or Start on network availability changed.
        private void onNetworkChanged(bool available) {
            appendInfoMsg($"Network Availability Change to ->  {available}");
            if (!available) {
                stopImpl(false, true);
            } else if (!IsRunning) {
                start();
            }
        }

        private void downloadStatusUpdated(bool downloadRunning) {
            if (!downloadRunning) IsStreaming = false;
            appendInfoMsg($"Flv download {(downloadRunning ? "started" : "stopped")}.");
            // this.stop();
        }

        private void onDanmaku(DanmakuModel danmaku) {
            if (!IsRunning/*May not come here*/) return;
            //TODO something here
            checkLiveStatus(danmaku);
            if (IsStreaming && danmakuStorage != null && danmakuStorage.IsWriting) {
                danmakuStorage.enqueue(danmaku);
            }
            //emit
            forEachByTaskWithDebug (DanmakuResolvers, resolver => {
                resolver.Invoke (danmaku);
            });
        }

        private void checkLiveStatus (DanmakuModel danmaku) {
            if (MsgTypeEnum.LiveEnd == danmaku.MsgType) {
                cancelMgr.cancel ("autostart-fetch");
                cancelMgr.remove ("autostart-fetch");
                flvFetcher.stop ();
                danmakuStorage?.stop ();
                //Update live status
                updateLiveStatus(false);
                appendInfoMsg("Message received : Live End.");
            } else if (MsgTypeEnum.LiveStart == danmaku.MsgType) {
                //Update live status
                updateLiveStatus(true);
                appendInfoMsg("Message received : Live Start.");
                if (settings.AutoStart && !flvFetcher.IsRunning) {
                    var cancellation = new CancellationTokenSource (requestTimeout);
                    cancelMgr.cancel ("autostart-fetch");
                    cancelMgr.set ("autostart-fetch", cancellation);
                    Task.Run (async () => {
                        var isUpdated = await settings.refreshAllAsync ();
                        if (isUpdated && IsRunning && IsLiveOn) {
                            //Ensure downloader's newest state
                            flvFetcher.start (settings.FlvAddress);
                            appendInfoMsg ($"Flv address updated : {settings.FlvAddress}");
                        }
                        cancelMgr.remove ("autostart-fetch");
                    }, cancellation.Token).ContinueWith (task => {
                        printException(task.Exception);
                    }, TaskContinuationOptions.OnlyOnFaulted);
                }
            }
        }

        private void updateLiveStatus(bool isLiveOn, bool raiseEvent = true) {
            if(this.isLiveOn != isLiveOn) {
                this.isLiveOn = isLiveOn;
                if (raiseEvent) {
                    forEachByTaskWithDebug(LiveDataResolvers, resolver => {
                        resolver.onStatusUpdate(isLiveOn);
                    });
                }
            }
        }

        private void reconnectOnError (Exception e) {
            //TODO something here
            appendErrorMsg (e?.Message);
            //Cancel exist reconnect action
            cancelMgr.cancel("danmaku-server-fetch");
            if (!IsRunning) {//donnot reconnect when download stopped.
                return;
            }
            appendInfoMsg ("Trying to reconnect to the danmaku server after " + delayReconnectMs);
            //set cancellation and start task.
            var cancellation = new CancellationTokenSource();
            cancelMgr.set("danmaku-server-fetch", cancellation);
            Task.Run (async() => {
                bool connectionOK = false;
                long used = 3000;
                await Task.Run(() => {
                    var sw = Stopwatch.StartNew();
                    connectionOK = network.checkCanConnect("live.bilibili.com");
                    sw.Stop();
                    used = sw.ElapsedMilliseconds;
                }, new CancellationTokenSource(3000).Token);
                if (delayReconnectMs > used) {
                    await Task.Delay(TimeSpan.FromMilliseconds(delayReconnectMs - used));
                }
                if (connectionOK) {//increase delay only on good network
                    delayReconnectMs += 1000;
                }
                tryConnect(danmakuClient, biliApi, settings.RealRoomId);
                cancelMgr.remove("danmaku-server-fetch");
            }, cancellation.Token).ContinueWith (task => {
                printException(task.Exception);
            }, TaskContinuationOptions.OnlyOnFaulted);
        }

        private void hotUpdated (long popularity) {
            forEachByTaskWithDebug (LiveDataResolvers, resolver => {
                resolver.onHotUpdate (popularity);
            });
        }

        //Raise event when video info checked
        private void videoChecked (VideoInfo info) {
            var previous = this.videoInfo;
            this.videoInfo = info;
            if (previous.BitRate != info.BitRate) {
                appendInfoMsg ($"{previous.BitRate} {info.BitRate}");
                var text = info.BitRate / 1000 + " Kbps";
                forEachByTaskWithDebug (LiveDataResolvers, resolver => {
                    resolver.onBitRateUpdate (info.BitRate, text);
                });
            }
            if (previous.Duration != info.Duration) {
                var text = formatTime (info.Duration);
                forEachByTaskWithDebug (LiveDataResolvers, resolver => {
                    resolver.onDurationUpdate (info.Duration, text);
                });
            }
        }

        private void onStreaming (long bytes) {
            if (bytes < 2 || IsStreaming) return;
            IsStreaming = true;
            appendInfoMsg ("Streaming check.....");
            if (flvFetcher != null) {
                flvFetcher.BytesReceived -= onStreaming;
            }
            Task.Run (() => {
                danmakuStorage?.stop (force : true);
                //Generate danmaku storage
                if (settings.DanmakuNeed) {
                    string xmlPath = Path.ChangeExtension (settings.FileFullName, "xml");
                    var startTimestamp = Convert.ToInt64 (TimeHelper.totalMsToGreenTime (DateTime.UtcNow));
                    danmakuStorage = new DanmakuStorage (xmlPath, startTimestamp, Encoding.UTF8);
                    danmakuStorage.startAsync ();
                    appendInfoMsg ("Start danmaku storage.....");
                }
                //Start connect to danmaku server
                if (!danmakuClient.isActive ()) {
                    appendInfoMsg ("Connect to danmaku server.....");
                    tryConnect(danmakuClient, biliApi, settings.RealRoomId);
                }
            });
            forEachByTaskWithDebug (StatusBinders, binder => {
                binder.onStreaming ();
            });
        }

        private void downloadSizeUpdated (long totalBytes) {
            RecordSize = totalBytes;
            //OnDownloadSizeUpdate
            var text = totalBytes.ToFileSize ();
            forEachByTaskWithDebug (LiveDataResolvers, resolver => {
                resolver.onDownloadSizeUpdate (totalBytes, text);
            });
        }

        private void tryConnect(DanmakuClient client, BiliApi biliApi, int realRoomId) {
            BiliApi.ServerBean bean = null;
            if (biliApi.tryGetValidDmServerBean(realRoomId.ToString(), out bean)){
                client.start(bean.Host, bean.Port, realRoomId);
            }
        }

        #region Help methods below
        //Help methods
        public void appendLine (string tag, string log) {
            forEachByTaskWithDebug (Loggers, logger => {
                logger.appendLine (tag, log);
            });
        }
        private void appendErrorMsg (string msg) => appendLine ("ERROR", msg);
        private void appendInfoMsg (string msg) => appendLine ("INFO", msg);
        
        private void printException (Exception e) {
            if (e == null) return;
            e.printStackTrace ();
            appendErrorMsg(e.Message);
        }

        private void forEachByTaskWithDebug<T> (LowList<T> host, Action<T> action) where T : class {
            Task.Run(() => {
                host.forEachEx(action, error => {
                    System.Diagnostics.Debug.WriteLine($"[{typeof(T).Name}]-" + error.Message);
                });
            }).ContinueWith( task => {
                printException(task.Exception);
            }, TaskContinuationOptions.OnlyOnFaulted);
        }

        private string formatTime (long ms) {
            return new System.Text.StringBuilder ()
                .Append ((ms / (1000 * 60 * 60)).ToString ("00")).Append (":")
                .Append ((ms / (1000 * 60) % 60).ToString ("00")).Append (":")
                .Append ((ms / 1000 % 60).ToString ("00")).ToString ();
        }

        #endregion

    }


}