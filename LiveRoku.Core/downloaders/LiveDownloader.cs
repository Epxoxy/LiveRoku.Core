using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LiveRoku.Base;

namespace LiveRoku.Core {

    //Danmaku provider
    public interface IDanmakuSource {
        LowList<DanmakuResolver> DanmakuResolvers { get; }
    }
    public class LiveDownloader : ILiveDownloader, ILogger, IDanmakuSource, IDisposable {
        public LowList<ILiveDataResolver> LiveDataResolvers { get; private set; }
        public LowList<IStatusBinder> StatusBinders { get; private set; }
        public LowList<DanmakuResolver> DanmakuResolvers { get; private set; }
        public LowList<ILogger> Loggers { get; private set; }
        public bool IsRunning { get; private set; }
        public bool IsStreaming { get; private set; }
        public LiveStatus LiveStatus { get; private set; }
        public long RecordSize { get; private set; }

        private BiliApi biliApi; //API access
        private FlvDownloader flvDloader; //Download flv video
        private DanmakuClient danmakuClient; //Download danmaku
        private DanmakuStorage danmakuStorage;
        private IRequestModel model; //Provide base parameters
        private VideoInfo video;
        private int realRoomId;
        private bool autoStart;
        private bool danmakuNeed;
        private string storeFolder;
        private string videoFullName;
        private int requestTimeout;

        public LiveDownloader (IRequestModel model, string userAgent, int requestTimeout = 5000) {
            this.StatusBinders = new LowList<IStatusBinder> ();
            this.DanmakuResolvers = new LowList<DanmakuResolver> ();
            this.LiveDataResolvers = new LowList<ILiveDataResolver> ();
            this.Loggers = new LowList<ILogger> ();
            this.biliApi = new BiliApi (this, userAgent);
            this.requestTimeout = requestTimeout;
            this.model = model;
        }

        public void Dispose () {
            if (IsRunning) {
                stop ();
            }
            clear (StatusBinders);
            clear (DanmakuResolvers);
            clear (LiveDataResolvers);
        }

        public void appendLine (string tag, string log) {
            forEachByTaskWithDebug (Loggers, logger => {
                logger.appendLine (tag, log);
            });
        }

        public void stop (bool force = false) {
            if (IsRunning) {
                IsRunning = false;
                IsStreaming = false;
                if (flvDloader != null) {
                    var temp = flvDloader;
                    flvDloader = null;
                    temp.BytesReceived -= downloadSizeUpdated;
                    temp.BytesReceived -= onStreaming;
                    temp.VideoInfoChecked -= videoChecked;
                    temp.StatusUpdated -= onDownloadStatus;
                    temp.stop ();
                }
                if (danmakuClient != null) {
                    var temp = danmakuClient;
                    danmakuClient = null;
                    temp.Events.ErrorLog -= logErrorMsg;
                    temp.Events.DanmakuReceived -= filterDanmaku;
                    temp.Events.HotUpdated -= onlineUpdated;
                    temp.Events.Closed -= checkIfReconnect;
                    temp.stop ();
                }
                if (danmakuStorage != null) {
                    var temp = danmakuStorage;
                    danmakuStorage = null;
                    temp.stop (force);
                }
                forEachByTaskWithDebug (StatusBinders, binder => {
                    binder.onStopped ();
                });
            }
        }

        public void start (string ingore = null) {
            if (IsRunning) return;
            IsRunning = true;
            IsStreaming = false;
            RecordSize = 0;
            video = new VideoInfo ();
            LiveStatus = LiveStatus.Unchecked;
            //Preparing signal
            forEachByTaskWithDebug (StatusBinders, binder => {
                binder.onPreparing ();
            });
            //Get running parameters
            var roomId = model.RoomId;
            var folder = model.Folder;
            var format = model.FileFormat;
            this.danmakuNeed = model.DownloadDanmaku;
            this.autoStart = model.AutoStart;
            //Prepare to start task
            //Cancel when no result back over five second
            var cts = new CancellationTokenSource (requestTimeout);
            //Prepare real roomId and flv url
            string roomIdTemp = null, flvUrl = null;
            Task.Run (() => {
                //Try to get real roomId
                roomIdTemp = biliApi.getRealRoomId (roomId);
                //Try to get flv url
                if (!string.IsNullOrEmpty (roomIdTemp)) {
                    flvUrl = biliApi.getRealUrl (roomIdTemp);
                }
            }, cts.Token).ContinueWith (task => {
                task.Exception?.printStackTrace ();
                //Check if get it successful
                if (!string.IsNullOrEmpty (flvUrl) &&
                    int.TryParse (roomIdTemp, out realRoomId)) {
                    var path = Path.Combine (folder, model.formatFileName (roomIdTemp));
                    storeUsing (folder, path);
                    //Create DanmakuLoader and subscribe event handlers
                    danmakuClient = new DanmakuClient ();
                    danmakuClient.Events.ErrorLog += logErrorMsg;
                    danmakuClient.Events.DanmakuReceived += filterDanmaku;
                    danmakuClient.Events.HotUpdated += onlineUpdated;
                    danmakuClient.Events.Closed += checkIfReconnect;
                    //Create FlvDloader and subscribe event handlers
                    flvDloader = new FlvDownloader (biliApi.userAgent, path);
                    flvDloader.BytesReceived += downloadSizeUpdated;
                    flvDloader.BytesReceived += onStreaming;
                    flvDloader.VideoInfoChecked += videoChecked;
                    flvDloader.StatusUpdated += onDownloadStatus;
                    //All parameters ready
                    forEachByTaskWithDebug (StatusBinders, binder => {
                        binder.onWaiting ();
                    });
                    logInfoMsg ($"All ready, fetch: {flvUrl}");
                    //All ready, start now
                    flvDloader.start (flvUrl);
                    danmakuClient.start (biliApi, realRoomId);
                } else {
                    logErrorMsg ($"Get value fail, RealRoomId : {roomIdTemp}");
                }
            });
        }

        private void storeUsing (string folder, string fileFullName) {
            this.storeFolder = folder;
            this.videoFullName = fileFullName;
        }

        private void onDownloadStatus (bool isRunning) {
            if (!isRunning) IsStreaming = false;
            // this.stop();
        }

        private void filterDanmaku (DanmakuModel danmaku) {
            if (!IsRunning) return;
            //TODO something here
            if (MsgTypeEnum.LiveEnd == danmaku.MsgType) {
                flvDloader?.stop ();
                danmakuStorage?.stop ();
                //Update live status
                LiveStatus = LiveStatus.End;
                //Raise event when live status updated.
                forEachByTaskWithDebug (LiveDataResolvers, resolver => {
                    resolver.onLiveStatusUpdate (LiveStatus.End);
                });
            } else if (MsgTypeEnum.LiveStart == danmaku.MsgType) {
                //Update live status
                LiveStatus = LiveStatus.Start;
                //Raise event when live status updated.
                forEachByTaskWithDebug (LiveDataResolvers, resolver => {
                    resolver.onLiveStatusUpdate (LiveStatus.Start);
                });
            }
            forEachByTaskWithDebug (DanmakuResolvers, resolver => {
                //TODO something
                resolver.Invoke (danmaku);
            });
        }

        private void checkIfReconnect (Exception e) {
            //TODO something here
            logErrorMsg (e?.Message);
            if (!IsRunning) return;
            logInfoMsg ("Trying to reconnect to the danmaku server.");
            Task.Run (() => {
                danmakuClient.start (biliApi, realRoomId);
            }).ContinueWith (task => {
                task.Exception?.printStackTrace ();
            });
        }

        private void onlineUpdated (long online) {
            forEachByTaskWithDebug (LiveDataResolvers, resolver => {
                resolver.onOnlineCountUpdate (online);
            });
        }

        //Raise event when video info checked
        private void videoChecked (VideoInfo video) {
            if (this.video.BitRate != video.BitRate) {
                var text = video.BitRate / 1000 + " Kbps";
                forEachByTaskWithDebug (LiveDataResolvers, resolver => {
                    resolver.onBitRateUpdate (video.BitRate, text);
                });
            }
            if (this.video.Duration != video.Duration) {
                var text = toTimeFormat (video.Duration);
                forEachByTaskWithDebug (LiveDataResolvers, resolver => {
                    resolver.onDurationUpdate (video.Duration, text);
                });
            }
            this.video = video;
        }

        private void onStreaming (long bytes) {
            if (bytes < 2 || IsStreaming) return;
            IsStreaming = true;
            logInfoMsg ("Streaming check.....");
            if (flvDloader != null) {
                flvDloader.BytesReceived -= onStreaming;
            }
            Task.Run (() => {
                danmakuStorage?.stop (force : true);
                //Generate danmaku storage
                if (danmakuNeed) {
                    string xmlPath = Path.ChangeExtension (videoFullName, "xml");
                    var startTimestamp = Convert.ToInt64 (TimeHelper.totalMsToGreenTime (DateTime.UtcNow));
                    danmakuStorage = new DanmakuStorage (xmlPath, startTimestamp, this, Encoding.UTF8);
                    danmakuStorage.start ();
                    logInfoMsg ("Start danmaku storage.....");
                }
                //Start connect to danmaku server
                Task.Run (() => {
                    if (danmakuClient.isActive ()) return;
                    logInfoMsg ("Connect danmaku server.....");
                    danmakuClient.start (biliApi, realRoomId);
                }).ContinueWith (task => {
                    task.Exception?.printStackTrace ();
                });
                forEachByTaskWithDebug (StatusBinders, binder => {
                    binder.onStreaming ();
                });
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

        private void logErrorMsg (string msg) => appendLine ("Error", msg);
        private void logInfoMsg (string msg) => appendLine ("INFO", msg);

        #region Help methods below
        //Help methods
        private void logIfException (Exception e) {
            if (e == null) return;
            e.printStackTrace ();
        }

        private void clear<T> (LowList<T> list) where T : class {
            if (list != null) {
                var temp = list;
                list = null;
                temp.clear ();
            }
        }

        private void forEachByTaskWithDebug<T> (LowList<T> host, Action<T> action) where T : class {
            Task.Run (() => {
                host.forEachEx (action, error => {
                    System.Diagnostics.Debug.WriteLine ($"[{typeof (T).Name}]-" + error.Message);
                });
            }).ContinueWith (task => {
                task.Exception?.printStackTrace ();
            });
        }

        private string toTimeFormat (long ms) {
            return new System.Text.StringBuilder ()
                .Append ((ms / (1000 * 60 * 60)).ToString ("00")).Append (":")
                .Append ((ms / (1000 * 60) % 60).ToString ("00")).Append (":")
                .Append ((ms / 1000 % 60).ToString ("00")).ToString ();
        }
        #endregion

    }

}