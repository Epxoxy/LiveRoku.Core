using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LiveRoku.Base;
namespace LiveRoku.Core {
    internal interface ILiveEventEmitter : ILiveProgressBinder, IStatusBinder {
        ILogger Logger { get; }
        void danmakuRecv (DanmakuModel danmaku);
    }

    public class LiveFetcher : ILiveFetcher, ILiveEventEmitter, IDisposable {
        private readonly CancellationManager cancelMgr = new CancellationManager ();
        private readonly Dictionary<string, object> extras = new Dictionary<string, object> ();
        private readonly INetworkWatcher network = new NetworkWatcherProxy ();
        private readonly BiliApi biliApi; //API access
        private readonly IFetchSettings settings; //Provide base parameters
        private readonly FetchArgsBean argsTemp;
        private readonly DanmakuCenter dmConnector;
        private readonly LiveDownloaderImpl downloader;
        private readonly int requestTimeout;

        public LiveFetcher (IFetchSettings original, string userAgent, int requestTimeout) {
            this.StatusBinders = new LowList<IStatusBinder> ();
            this.DanmakuHandlers = new LowList<DanmakuResolver> ();
            this.LiveProgressBinders = new LowList<ILiveProgressBinder> ();
            this.Logger = new SimpleLogger ();
            //Initialize Others
            this.biliApi = new BiliApi (Logger, userAgent);
            this.dmConnector = new DanmakuCenter (this, biliApi);
            this.downloader = new LiveDownloaderImpl (this, userAgent);
            this.argsTemp = new FetchArgsBean (-1, biliApi, Logger);
            this.requestTimeout = requestTimeout;
            this.settings = original;
        }
        public LiveFetcher (IFetchSettings original, string userAgent):
            this (original, userAgent, 5000) { }

        public RoomInfo fetchRoomInfo (bool refresh) {
            if (!IsRunning || refresh) {
                int roomId;
                if (!int.TryParse (settings.RoomId, out roomId)) {
                    return null;
                }
                if (argsTemp.OriginRoomId != roomId) {
                    argsTemp.resetOriginId (roomId);
                }
                argsTemp.fetchRoomInfo ();
            }
            return argsTemp.RoomInfo;
        }

        //For extension
        public void bindState (string key, Action<Dictionary<string, object>> doWhat) {
            //TODO implement
        }

        public void putExtra (string key, object value) {
            if (extras.ContainsKey (key)) {
                extras[key] = value;
            } else {
                extras.Add (key, value);
            }
        }

        public object getExtra (string key) {
            if (extras.ContainsKey (key)) return extras[key];
            return null;
        }

        public void Dispose () {
            dmConnector.purgeEvents ();
            StatusBinders.clear ();
            DanmakuHandlers.clear ();
            LiveProgressBinders.clear ();
            stop ();
        }

        public void stop (bool force = false) => stopImpl (force, false);

        public void start (string ingore = null) {
            if (IsRunning) return;
            //Basic initialize
            network.assumeAvailability (true);
            network.attach (onNetworkChanged);
            IsRunning = true;
            downloader.reset ();
            dmConnector.resetState ();
            //Preparing signal
            this.onPreparing ();
            //Basic parameters check
            //Get running parameters
            var roomIdText = settings.RoomId;
            var folder = settings.Folder;
            var fileNameFormat = settings.FileFormat;
            int roomId;
            if (!int.TryParse (roomIdText, out roomId) || roomId <= 0) {
                Logger.log (Level.Error, "Wrong id.");
                stop ();
                return;
            }
            startImpl (roomId, folder, fileNameFormat, !isValueTrue (extras, "flv-needless"));
        }

        //Internal Impl
        private void stopImpl (bool force, bool internalCall) {
            Debug.WriteLine ("stopImpl invoke");
            if (!internalCall) { //Network watcher needless
                network.detach (); //detach now
            }
            cancelMgr.cancelAll ();
            cancelMgr.clear ();
            if (IsRunning) {
                IsRunning = false;
                dmConnector.disconnect ();
                downloader.stop (force);
                Logger.log (Level.Info, "Downloader stopped.");
                this.onStopped ();
            }
        }

        //Internal Impl
        private void startImpl (int roomId, string folder, string fileNameFormat, bool videoRequire) {
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
                    var fileName = getFileFullName (argsTemp.FileNameFormat, argsTemp, DateTime.Now);
                    runOnlyOne (() => {
                        argsTemp.fetchRoomInfo ();
                    }, "fetch-room-info");
                    //All ready, start now
                    Logger.log (Level.Info, $"All ready, fetch: {argsTemp.FlvAddress}");
                    //All parameters ready
                    this.onWaiting ();
                    if (argsTemp.VideoRequire) {
                        downloader.download (argsTemp.FlvAddress, fileName, argsTemp.DanmakuRequire);
                    } else {
                        Logger.log (Level.Info, $"No video mode");
                    }
                    dmConnector.connect (argsTemp.RealRoomId);
                } else {
                    Logger.log (Level.Error, $"Get value fail, RealRoomId : {argsTemp.RealRoomIdText}");
                    stop ();
                }
                cancelMgr.remove ("fetch-start-impl");
            }, startCtl.Token);
        }

        //Stop or Start on network availability changed.
        private void onNetworkChanged (bool available) {
            Logger.log (Level.Info, $"Network Availability Change to ->  {available}");
            if (!available) {
                stopImpl (false, true);
            } else if (!IsRunning) {
                start ();
            }
        }

        private void danmakuRecvInternal (DanmakuModel danmaku) {
            downloader.danmakuToLocal (danmaku);
        }

        private void onStreamingInternal () {
            runOnlyOne (() => {
                if (dmConnector.IsConnected) return;
                dmConnector.connect (argsTemp.RealRoomId);
            }, nameof (onStreamingInternal));
        }

        [SuppressMessage ("Microsoft.Performance", "CS4014")]
        private void onLiveStatusInternal (bool isOn) {
            if (!argsTemp.VideoRequire) return;
            if (!isOn) {
                cancelMgr.cancel (nameof (onLiveStatusInternal));
                cancelMgr.remove (nameof (onLiveStatusInternal));
                downloader.stop (false);
            } else {
                if (argsTemp.AutoStart && !downloader.IsStreaming) {
                    runOnlyOne (() => {
                        var isUpdated = argsTemp.fetchUrlAndRealId ();
                        Logger.log (Level.Info, $"Flv address updated : {argsTemp.FlvAddress}");
                        if (isUpdated && IsRunning && dmConnector.IsLiveOn && !downloader.IsStreaming) {
                            //Ensure downloader's newest state
                            var fileName = getFileFullName (argsTemp.FileNameFormat, argsTemp, DateTime.Now);
                            downloader.download (argsTemp.FlvAddress, fileName, argsTemp.DanmakuRequire);
                        }
                    }, nameof (onLiveStatusInternal), requestTimeout).ContinueWith (task => {
                        task.Exception?.printOn (Logger);
                    }, TaskContinuationOptions.OnlyOnFaulted);
                }
            }
        }

        #region Help methods below

        private string getFileFullName (string format, FetchArgsBean args, DateTime baseTime) {
            var folder = args.Folder;
            var roomId = args.RealRoomIdText;
            var fileName = string.Empty;
            try {
                fileName = format.Replace ("{roomId}", roomId)
                    .Replace ("{Y}", baseTime.Year.ToString ("D4"))
                    .Replace ("{M}", baseTime.Month.ToString ("D2"))
                    .Replace ("{d}", baseTime.Day.ToString ("D2"))
                    .Replace ("{H}", baseTime.Hour.ToString ("D2"))
                    .Replace ("{m}", baseTime.Minute.ToString ("D2"))
                    .Replace ("{s}", baseTime.Second.ToString ("D2"));
            } catch (Exception e) {
                e.printStackTrace ();
                fileName = $"{roomId}-{baseTime.ToString("yyyy-MM-dd-HH-mm-ss")}";
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

        private bool isValueTrue (Dictionary<string, object> dict, string key) {
            return dict.ContainsKey (key) && dict[key] is bool && ((bool) dict[key]);
        }

        #endregion

        //..................
        //Boardcast events
        //Interface part below
        public void onStatusUpdate (bool isOn) {
            onLiveStatusInternal (isOn);
            LiveProgressBinders.forEachHideExAsync (binder => {
                binder.onStatusUpdate (isOn);
            }, Logger);
        }
        public void onDurationUpdate (long duration, string friendlyText) {
            LiveProgressBinders.forEachHideExAsync (binder => {
                binder.onDurationUpdate (duration, friendlyText);
            }, Logger);
        }
        public void onDownloadSizeUpdate (long totalSize, string friendlySize) {
            LiveProgressBinders.forEachHideExAsync (binder => {
                binder.onDownloadSizeUpdate (totalSize, friendlySize);
            }, Logger);
        }
        public void onBitRateUpdate (long bitRate, string bitRateText) {
            LiveProgressBinders.forEachHideExAsync (binder => {
                binder.onBitRateUpdate (bitRate, bitRateText);
            }, Logger);
        }
        public void onHotUpdate (long popularity) {
            LiveProgressBinders.forEachHideExAsync (binder => {
                binder.onHotUpdate (popularity);
            }, Logger);
        }
        public void onPreparing () {
            StatusBinders.forEachHideExAsync (binder => {
                binder.onPreparing ();
            }, Logger);
        }
        public void onStreaming () {
            onStreamingInternal ();
            StatusBinders.forEachHideExAsync (binder => {
                binder.onStreaming ();
            }, Logger);
        }
        public void onWaiting () {
            StatusBinders.forEachHideExAsync (binder => {
                binder.onWaiting ();
            }, Logger);
        }
        public void onStopped () {
            StatusBinders.forEachHideExAsync (binder => {
                binder.onStopped ();
            }, Logger);
        }
        public void danmakuRecv (DanmakuModel danmaku) {
            danmakuRecvInternal (danmaku);
            DanmakuHandlers.forEachHideExAsync (handler => {
                handler.Invoke (danmaku);
            }, Logger);
        }
        public void onMissionComplete (IMission mission) {
            LiveProgressBinders.forEachHideExAsync (binder => {
                binder.onMissionComplete (mission);
            }, Logger);
        }

        public LowList<ILiveProgressBinder> LiveProgressBinders { get; private set; }
        public LowList<IStatusBinder> StatusBinders { get; private set; }
        public LowList<DanmakuResolver> DanmakuHandlers { get; private set; }
        public ILogger Logger { get; private set; }
        public bool IsRunning { get; private set; }
        public bool IsStreaming => downloader.IsStreaming;
        public bool IsLiveOn => dmConnector.IsLiveOn;
    }

}