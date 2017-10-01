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
        LowList<DanmakuResolver> DanmakuHandlers { get; }
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

    public class LiveFetcher : ILiveFetcher, IDanmakuSource, IDisposable {
        public LowList<ILiveProgressBinder> LiveProgressBinders { get; private set; }
        public LowList<IStatusBinder> StatusBinders { get; private set; }
        public LowList<DanmakuResolver> DanmakuHandlers { get; private set; }
        public ILogger Logger { get; private set; }
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
        private readonly IFetchSettings original; //Provide base parameters
        private Action<DanmakuModel> onCheckLiveStatus;
        private VideoInfo videoInfo;
        private int requestTimeout;
        private FetchCacheBean settings;
        private bool isLiveOn;
        //settings
        private long delayReconnectMs = 500;
        private int retryTimes = 0;
        private int maxRetryTimes = 10;

        public LiveFetcher (IFetchSettings original, string userAgent, int requestTimeout) {
            cancelMgr = new CancellationManager ();
            //Initialize Downloaders
            this.danmakuClient = new DanmakuClient ();
            this.danmakuEvents = danmakuClient.Events;
            this.flvFetcher = new FlvDownloader (userAgent, null);
            //Initialize Handlers
            this.StatusBinders = new LowList<IStatusBinder> ();
            this.DanmakuHandlers = new LowList<DanmakuResolver> ();
            this.LiveProgressBinders = new LowList<ILiveProgressBinder> ();
            this.Logger = new SimpleLogger();
            //Initialize Others
            this.biliApi = new BiliApi (Logger, userAgent);
            this.requestTimeout = requestTimeout;
            this.original = original;
            //Subscribe events
            this.danmakuEvents.OnLog = msg => Logger.log(Level.Info, msg);
            this.danmakuEvents.DanmakuReceived = onDanmaku;
            this.danmakuEvents.HotUpdated = hotUpdated;
            this.danmakuEvents.Connected = onClientConnected;
            this.flvFetcher.VideoInfoChecked = videoChecked;
            this.flvFetcher.IsRunningUpdated = downloadStatusUpdated;
            this.flvFetcher.BytesReceived += downloadSizeUpdated;
        }
        public LiveFetcher (IFetchSettings original, string userAgent):
            this (original, userAgent, 5000) { }

        private void purgeEvents () {
            flvFetcher.BytesReceived -= onStreaming;
            flvFetcher.BytesReceived -= downloadSizeUpdated;
            flvFetcher.VideoInfoChecked = null;
            flvFetcher.IsRunningUpdated = null;
            danmakuEvents.purgeEvents ();
        }

        public RoomInfo fetchRoomInfo(bool refresh) {
            if (!IsRunning || refresh) {
                if (settings == null) {
                    int roomId;
                    if (!int.TryParse(original.RoomId, out roomId)) return null;
                    settings = new FetchCacheBean(roomId, biliApi);
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
        
        public void Dispose () {
            purgeEvents ();
            StatusBinders?.clear ();
            DanmakuHandlers?.clear ();
            LiveProgressBinders?.clear ();
            stop();
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
            delayReconnectMs = 500;
            retryTimes = 0;
            videoInfo = new VideoInfo ();
            updateLiveStatus(false, false);
            //Preparing signal
            forEachWithDebugAsync (StatusBinders, binder => {
                binder.onPreparing ();
            });
            //Basic parameters check
            //Get running parameters
            var roomIdText = original.RoomId;
            var folder = original.Folder;
            int roomId;
            if (!int.TryParse (roomIdText, out roomId)) {
                Logger.log(Level.Error,"Wrong id.");
                stop ();
                return;
            }
            startImpl(roomId, folder, !isKeyTrue(extras, "flv-needless"));
        }
        
        //Internal Impl
        private void stopImpl(bool force, bool internalCall) {
            if (!internalCall) {//Network watcher needless
                network.detach();//detach now
            }
            cancelMgr.cancelAll();
            cancelMgr.clear();
            if (IsRunning) {
                IsRunning = false;
                IsStreaming = false;
                danmakuEvents.Closed = null;
                flvFetcher.stop ();
                danmakuClient.stop ();
                danmakuStorage?.stop(force);
                Logger.log(Level.Info,"Downloader stopped.");
                forEachWithDebugAsync (StatusBinders, binder => {
                    binder.onStopped ();
                });
            }
        }
        
        //Internal Impl
        private void startImpl(int roomId, string folder, bool videoNeed) {
            //Prepare to start task
            //Cancel when no result back over five second
            var cts = new CancellationTokenSource (requestTimeout);
            //Prepare real roomId and flv url
            bool isUpdated = false;
            Task.Run (async () => {
                settings = new FetchCacheBean (roomId, biliApi);
                settings.Logger = Logger;
                isUpdated = await settings.refreshAllAsync ();
            }, cts.Token).ContinueWith (task => {
                printException(task.Exception);
                //Check if get it successful
                if (isUpdated) {
                    settings.fetchRoomInfoAsync();
                    var fileName = formatFileName(folder, settings.RealRoomIdText, original);
                    //complete model
                    settings.Folder = folder;
                    settings.FileFullName = fileName;
                    settings.AutoStart = original.AutoStart;
                    settings.DanmakuNeed = original.DownloadDanmaku;
                    //All ready, start now
                    if (videoNeed) {
                        //Create FlvDloader and subscribe event handlers
                        flvFetcher.updateSavePath(settings.FileFullName);
                        flvFetcher.BytesReceived -= onStreaming;
                        flvFetcher.BytesReceived += onStreaming;
                        onCheckLiveStatus = checkLiveStatusForDownload;
                    } else {
                        onCheckLiveStatus = updateLiveStatusOnly;
                    }
                    danmakuEvents.Closed = reconnectOnError;
                    Logger.log(Level.Info,$"All ready, fetch: {settings.FlvAddress}");
                    //All parameters ready
                    forEachWithDebugAsync(StatusBinders, binder => {
                        binder.onWaiting();
                    });
                    if(videoNeed) {
                        flvFetcher.start(settings.FlvAddress);
                    }
                    Task.Run(() => {
                        tryConnect(danmakuClient, biliApi, settings.RealRoomId);
                    });
                } else {
                    Logger.log(Level.Error,$"Get value fail, RealRoomId : {settings.RealRoomIdText}");
                    stop ();
                }
            });
        }
        


        //Stop or Start on network availability changed.
        private void onNetworkChanged(bool available) {
            Logger.log(Level.Info,$"Network Availability Change to ->  {available}");
            if (!available) {
                stopImpl(false, true);
            } else if (!IsRunning) {
                start();
            }
        }

        private void downloadStatusUpdated(bool downloadRunning) {
            if (!downloadRunning) IsStreaming = false;
            Logger.log(Level.Info,$"Flv download {(downloadRunning ? "started" : "stopped")}.");
            // this.stop();
        }

        private void onDanmaku(DanmakuModel danmaku) {
            if (!IsRunning/*May not come here*/) return;
            //TODO something here
            onCheckLiveStatus?.Invoke(danmaku);
            if (IsStreaming && danmakuStorage != null && danmakuStorage.IsWriting) {
                danmakuStorage.enqueue(danmaku);
            }
            //emit
            forEachWithDebugAsync (DanmakuHandlers, resolver => {
                resolver.Invoke (danmaku);
            });
        }

        private void updateLiveStatusOnly(DanmakuModel danmaku) {
            if (MsgTypeEnum.LiveEnd == danmaku.MsgType) {
                //Update live status
                updateLiveStatus(false);
                Logger.log(Level.Info,"Message received : Live End.");
            } else if (MsgTypeEnum.LiveStart == danmaku.MsgType) {
                //Update live status
                updateLiveStatus(true);
                Logger.log(Level.Info,"Message received : Live Start.");
            }
        }

        private void checkLiveStatusForDownload (DanmakuModel danmaku) {
            if (MsgTypeEnum.LiveEnd == danmaku.MsgType) {
                cancelMgr.cancel ("autostart-fetch");
                cancelMgr.remove ("autostart-fetch");
                flvFetcher.stop ();
                danmakuStorage?.stop ();
                //Update live status
                updateLiveStatus(false);
                Logger.log(Level.Info,"Message received : Live End.");
            } else if (MsgTypeEnum.LiveStart == danmaku.MsgType) {
                //Update live status
                updateLiveStatus(true);
                Logger.log(Level.Info,"Message received : Live Start.");
                if (settings.AutoStart && !flvFetcher.IsRunning) {
                    var cancellation = new CancellationTokenSource (requestTimeout);
                    cancelMgr.cancel ("autostart-fetch");
                    cancelMgr.set ("autostart-fetch", cancellation);
                    Task.Run (async () => {
                        var isUpdated = await settings.refreshAllAsync ();
                        if (isUpdated && IsRunning && IsLiveOn) {
                            //Ensure downloader's newest state
                            var fileName = formatFileName(settings.Folder, settings.RealRoomIdText, original);
                            settings.FileFullName = fileName;
                            flvFetcher.updateSavePath(fileName);
                            flvFetcher.start (settings.FlvAddress);
                            Logger.log(Level.Info,$"Flv address updated : {settings.FlvAddress}");
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
                    forEachWithDebugAsync(LiveProgressBinders, resolver => {
                        resolver.onStatusUpdate(isLiveOn);
                    });
                }
            }
        }

        private void onClientConnected() {
            Logger.log(Level.Info,$"Connect to danmaku server ok.");
            delayReconnectMs = 500;
            retryTimes = 0;
        }

        private void reconnectOnError (Exception e) {
            //TODO something here
            Logger.log(Level.Error,e?.Message);
            //Cancel exist reconnect action
            cancelMgr.cancel("danmaku-server-fetch");
            if (!IsRunning) {//donnot reconnect when download stopped.
                return;
            }
            if (retryTimes > maxRetryTimes) {
                Logger.log(Level.Error,"Retry time more than the max.");
                return;
            }
            //set cancellation and start task.
            var cancellation = new CancellationTokenSource();
            cancelMgr.set("danmaku-server-fetch", cancellation);
            bool connectionOK = false;
            long used = 3000;
            var cts = new CancellationTokenSource(3000);
            Task.Run (() => {
                var sw = Stopwatch.StartNew();
                connectionOK = network.checkCanConnect("live.bilibili.com");
                sw.Stop();
                used = sw.ElapsedMilliseconds;
            }, cts.Token).ContinueWith(async task => {
                task.Exception?.printStackTrace();
                if (delayReconnectMs > used) {
                    await Task.Delay(TimeSpan.FromMilliseconds(delayReconnectMs - used));
                    Logger.log(Level.Info,$"Trying to reconnect to danmaku server after {(delayReconnectMs - used) / 1000d}s");
                }
                //increase delay
                delayReconnectMs += (connectionOK ? 1000 : retryTimes * 2000);
                retryTimes++;
                tryConnect(danmakuClient, biliApi, settings.RealRoomId);
                cancelMgr.remove("danmaku-server-fetch");
            });
        }

        private void hotUpdated (long popularity) {
            forEachWithDebugAsync (LiveProgressBinders, resolver => {
                resolver.onHotUpdate (popularity);
            });
        }

        //Raise event when video info checked
        private void videoChecked (VideoInfo info) {
            var previous = this.videoInfo;
            this.videoInfo = info;
            if (previous.BitRate != info.BitRate) {
                Logger.log(Level.Info,$"{previous.BitRate} {info.BitRate}");
                var text = info.BitRate / 1000 + " Kbps";
                forEachWithDebugAsync (LiveProgressBinders, resolver => {
                    resolver.onBitRateUpdate (info.BitRate, text);
                });
            }
            if (previous.Duration != info.Duration) {
                var text = formatTime (info.Duration);
                forEachWithDebugAsync (LiveProgressBinders, resolver => {
                    resolver.onDurationUpdate (info.Duration, text);
                });
            }
        }

        private void onStreaming (long bytes) {
            if (bytes < 2 || IsStreaming) return;
            IsStreaming = true;
            Logger.log(Level.Info,"Streaming check.....");
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
                    Logger.log(Level.Info,"Start danmaku storage.....");
                }
                //Start connect to danmaku server
                if (!danmakuClient.isActive ()) {
                    Logger.log(Level.Info,"Connect to danmaku server.....");
                    tryConnect(danmakuClient, biliApi, settings.RealRoomId);
                }
            });
            forEachWithDebugAsync (StatusBinders, binder => {
                binder.onStreaming ();
            });
        }

        private void downloadSizeUpdated (long totalBytes) {
            RecordSize = totalBytes;
            //OnDownloadSizeUpdate
            var text = totalBytes.ToFileSize ();
            forEachWithDebugAsync(LiveProgressBinders, resolver => {
                resolver.onDownloadSizeUpdate (totalBytes, text);
            });
        }

        private void tryConnect(DanmakuClient client, BiliApi biliApi, int realRoomId) {
            Logger.log(Level.Info, "Trying to connect to damaku server.");
            BiliApi.ServerBean bean = null;
            if (biliApi.tryGetValidDmServerBean(realRoomId.ToString(), out bean)){
                client.start(bean.Host, bean.Port, realRoomId);
            } else {
                Logger.log(Level.Error, "Cannot get valid server address and port.");
            }
        }

        #region Help methods below
        //Help methods
        private string formatFileName(string folder, string roomId, IFetchSettings formatter) {
            return Path.Combine(folder, formatter.formatFileName(roomId));
        }

        private bool isKeyTrue(Dictionary<string, object> dict, string key) {
            return dict.ContainsKey(key) && dict[key] is bool && ((bool)dict[key]);
        }

        private void printException (Exception e) {
            if (e == null) return;
            e.printStackTrace ();
            Logger.log(Level.Error, e.Message);
        }

        private void forEachWithDebugAsync<T> (LowList<T> host, Action<T> action) where T : class {
            host.forEachSafelyAsync(action, error => {
                Debug.WriteLine($"[{typeof(T).Name}]-" + error.Message);
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