using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LiveRoku.Base;
using LiveRoku.Base.Logger;
namespace LiveRoku.Core {
    [SuppressMessage ("Microsoft.Performance", "CS4014")]
    public class LiveFetchManager : LiveEventEmitterBase, ILiveFetcher, IDisposable {
        public ISettingsBase Extra => extra;
        public bool IsRunning { get; private set; }
        public bool IsStreaming => downloader.IsStreaming;
        public bool IsLiveOn => chatMsg.IsLiveOn;

        private readonly ISettings extra = new EasySettings ();
        private readonly CancellationManager cancelMgr = new CancellationManager ();
        private readonly INetworkWatcher network = new NetworkWatcherProxy ();
        private readonly BiliApi biliApi; //API access
        private readonly IFetchArgsHost settings; //Provide base parameters
        private readonly FetchArgsBean argsTemp;
        private readonly ChatCenter chatMsg;
        private readonly LiveDownloaderImpl downloader;
        private readonly int requestTimeout;

        public LiveFetchManager (IFetchArgsHost settings, int requestTimeout) {
            //Initialize
            this.biliApi = new BiliApi (() => {
                return new StandardWebClient ();
            }, Logger, settings.UserAgent);
            this.chatMsg = new ChatCenter (this, biliApi);
            this.downloader = new LiveDownloaderImpl (this, settings.UserAgent);
            this.argsTemp = new FetchArgsBean (-1, biliApi, Logger);
            this.requestTimeout = requestTimeout;
            this.settings = settings;
        }
        public LiveFetchManager (IFetchArgsHost original):
            this (original, 5000) { }

        public void Dispose () {
            stop ();
            chatMsg.purgeEvents ();
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
            chatMsg.resetState ();
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

        private void cancelFetch (bool force, bool internalCall) {
            Debug.WriteLine ("stopImpl invoke");
            if (!internalCall) { //Network watcher needless
                network.detach (); //detach now
            }
            cancelMgr.cancelAll ();
            cancelMgr.clear ();
            if (IsRunning) {
                IsRunning = false;
                chatMsg.disconnect ();
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
                    chatMsg.connect (argsTemp.RealRoomId);
                } else {
                    Logger.log (Level.Error, $"Get value fail, RealRoomId : {argsTemp.RealRoomId}");
                    stop ();
                }
                cancelMgr.remove ("fetch-start-impl");
            }, startCtl.Token);
        }

        //.......
        //Handlers
        //.......
        protected override void onDanmakuRecvInternal (DanmakuModel danmaku) {
            downloader.danmakuToLocal (danmaku);
        }

        protected override void onStreamingInternal () {
            runOnlyOne (() => {
                if (chatMsg.IsConnected) return;
                chatMsg.connect (argsTemp.RealRoomId);
            }, nameof (onStreamingInternal));
        }

        protected override void onLiveStatusUpdateInternal (bool isOn) {
            if (!argsTemp.VideoRequire) return;
            if (!isOn) {
                cancelMgr.cancel (nameof (onLiveStatusUpdateInternal));
                cancelMgr.remove (nameof (onLiveStatusUpdateInternal));
                downloader.stop (false);
            } else if (argsTemp.AutoStart && !downloader.IsStreaming) {
                runOnlyOne (() => {
                    var isUpdated = argsTemp.fetchUrlAndRealId ();
                    Logger.log (Level.Info, $"Flv address updated : {argsTemp.FlvAddress}");
                    if (isUpdated && IsRunning && chatMsg.IsLiveOn && !downloader.IsStreaming) {
                        //Ensure downloader's newest state
                        var fileName = getFileFullName (argsTemp.FileNameFormat, argsTemp, DateTime.Now);
                        downloader.download (argsTemp.FlvAddress, fileName, argsTemp.DanmakuRequire);
                    }
                }, nameof (onLiveStatusUpdateInternal), requestTimeout).ContinueWith (task => {
                    task.Exception?.printOn (Logger);
                }, TaskContinuationOptions.OnlyOnFaulted);
            }
        }

        //...........
        //Help method
        //...........
        private string getFileFullName (string format, FetchArgsBean args, DateTime baseTime) {
            var fileName = string.Empty;
            try {
                fileName = format.Replace ("{roomId}", args.RealRoomId.ToString ())
                    .Replace ("{Y}", baseTime.Year.ToString ("D4"))
                    .Replace ("{M}", baseTime.Month.ToString ("D2"))
                    .Replace ("{d}", baseTime.Day.ToString ("D2"))
                    .Replace ("{H}", baseTime.Hour.ToString ("D2"))
                    .Replace ("{m}", baseTime.Minute.ToString ("D2"))
                    .Replace ("{s}", baseTime.Second.ToString ("D2"));
            } catch (Exception e) {
                e.printStackTrace ();
                fileName = $"{args.RealRoomId}-{baseTime.ToString("yyyy-MM-dd-HH-mm-ss")}";
            }
            return Path.Combine (args.Folder, fileName);
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