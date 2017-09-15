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
        public long RecordSize { get; private set; }
        public bool IsLiveOn { get; set; }

        private readonly CancellationManager cancelMgr;
        private readonly BiliApi biliApi; //API access
        private readonly FlvDownloader flvFetcher; //Download flv video
        private readonly DanmakuClient danmakuClient; //Download danmaku
        private DanmakuStorage danmakuStorage;
        private readonly IRequestModel model; //Provide base parameters
        private VideoInfo videoInfo;
        private int requestTimeout;
        private FetchBean settings;

        public LiveDownloader (IRequestModel model, string userAgent, int requestTimeout) {
            cancelMgr = new CancellationManager ();
            //Initialize Downloaders
            this.danmakuClient = new DanmakuClient ();
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
            this.danmakuClient.Events.ErrorLog += appendErrorMsg;
            this.danmakuClient.Events.DanmakuReceived += danmakuTransmit;
            this.danmakuClient.Events.HotUpdated += hotUpdated;
            this.flvFetcher.BytesReceived += downloadSizeUpdated;
            this.flvFetcher.VideoInfoChecked += videoChecked;
            this.flvFetcher.StatusUpdated += onDownloadStatus;
        }
        public LiveDownloader (IRequestModel model, string userAgent):
            this (model, userAgent, 5000) { }

        private void purgeEvents () {
            flvFetcher.BytesReceived -= downloadSizeUpdated;
            flvFetcher.BytesReceived -= onStreaming;
            flvFetcher.VideoInfoChecked -= videoChecked;
            flvFetcher.StatusUpdated -= onDownloadStatus;
            danmakuClient.Events.purgeEvents ();
        }

        public void Dispose () {
            stop ();
            purgeEvents ();
            StatusBinders?.clear ();
            DanmakuResolvers?.clear ();
            LiveDataResolvers?.clear ();
        }

        public void stop (bool force = false) {
            if (IsRunning) {
                IsRunning = false;
                IsStreaming = false;
                danmakuClient.Events.Closed -= reconnectOnError;
                flvFetcher.stop ();
                danmakuClient.stop ();
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
            videoInfo = new VideoInfo ();
            IsLiveOn = false;
            //Preparing signal
            forEachByTaskWithDebug (StatusBinders, binder => {
                binder.onPreparing ();
            });
            //Get running parameters
            var roomIdText = model.RoomId;
            var folder = model.Folder;
            var format = model.FileFormat;
            int roomId;
            if (!int.TryParse (roomIdText, out roomId)) {
                appendErrorMsg ("Wrong id.");
                stop ();
                return;
            }
            //Prepare to start task
            //Cancel when no result back over five second
            var cts = new CancellationTokenSource (requestTimeout);
            //Prepare real roomId and flv url
            bool isUpdated = false;
            Task.Run (async () => {
                settings = new FetchBean (roomId, biliApi);
                settings.Logger = this;
                isUpdated = await settings.refresh ();
            }, cts.Token).ContinueWith (task => {
                task.Exception?.printStackTrace ();
                //Check if get it successful
                if (isUpdated) {
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
                    danmakuClient.Events.Closed -= reconnectOnError;
                    danmakuClient.Events.Closed += reconnectOnError;
                    //All parameters ready
                    forEachByTaskWithDebug (StatusBinders, binder => {
                        binder.onWaiting ();
                    });
                    appendInfoMsg ($"All ready, fetch: {settings.FlvAddress}");
                    //All ready, start now
                    flvFetcher.start (settings.FlvAddress);
                    danmakuClient.start (biliApi, settings.RealRoomId);
                } else {
                    appendErrorMsg ($"Get value fail, RealRoomId : {settings.RealRoomIdText}");
                    stop ();
                }
            });
        }

        private void onDownloadStatus (bool isRunning) {
            if (!isRunning) IsStreaming = false;
            // this.stop();
        }

        private void danmakuTransmit (DanmakuModel danmaku) {
            if (!IsRunning/*May not come here*/) return;
            //TODO something here
            checkLiveStatus(danmaku);
            forEachByTaskWithDebug (DanmakuResolvers, resolver => {
                //TODO something
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
                IsLiveOn = false;
                //Raise event when live status updated.
                forEachByTaskWithDebug (LiveDataResolvers, resolver => {
                    resolver.onStatusUpdate (IsLiveOn);
                });
            } else if (MsgTypeEnum.LiveStart == danmaku.MsgType) {
                //Update live status
                IsLiveOn = true;
                if (settings.AutoStart && !flvFetcher.IsRunning) {
                    var cancellation = new CancellationTokenSource (requestTimeout);
                    cancelMgr.cancel ("autostart-fetch");
                    cancelMgr.set ("autostart-fetch", cancellation);
                    Task.Run (async () => {
                        var isUpdated = await settings.refresh ();
                        if (isUpdated && IsRunning && IsLiveOn) {
                            flvFetcher.start (settings.FlvAddress);
                            appendInfoMsg ($"Flv address updated : {settings.FlvAddress}");
                        }
                        cancelMgr.remove ("autostart-fetch");
                    }, cancellation.Token).ContinueWith (task => {
                        if (task.Exception != null) {
                            task.Exception.printStackTrace ();
                            appendErrorMsg (task.Exception.Message);
                        }
                    });
                }
                //Raise event when live status updated.
                forEachByTaskWithDebug (LiveDataResolvers, resolver => {
                    resolver.onStatusUpdate (IsLiveOn);
                });
            }
        }

        private void reconnectOnError (Exception e) {
            //TODO something here
            appendErrorMsg (e?.Message);
            if (!IsRunning) return;
            appendInfoMsg ("Trying to reconnect to the danmaku server.");
            Task.Run (() => {
                danmakuClient.start (biliApi, settings.RealRoomId);
            }).ContinueWith (task => {
                task.Exception?.printStackTrace ();
            });
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
                    danmakuStorage = new DanmakuStorage (xmlPath, startTimestamp, this, Encoding.UTF8);
                    danmakuStorage.startAsync ();
                    appendInfoMsg ("Start danmaku storage.....");
                }
                //Start connect to danmaku server
                if (!danmakuClient.isActive ()) {
                    appendInfoMsg ("Connect to danmaku server.....");
                    danmakuClient.start (biliApi, settings.RealRoomId);
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

        public RoomInfo fetchRoomInfo(bool refresh) {
            if (!IsRunning || refresh) {
                if (settings == null) {
                    int roomId;
                    if (!int.TryParse(model.RoomId, out roomId)) return null;
                    settings = new FetchBean(roomId, biliApi);
                }
                settings.refresh();
            }
            return settings.RoomInfo;
        }

        //For extension
        public object getExtra(string key) { return null; }

        public void appendLine (string tag, string log) {
            forEachByTaskWithDebug (Loggers, logger => {
                logger.appendLine (tag, log);
            });
        }
        private void appendErrorMsg (string msg) => appendLine ("Error", msg);
        private void appendInfoMsg (string msg) => appendLine ("INFO", msg);

        #region Help methods below
        //Help methods

        private void printException (Exception e) {
            if (e == null) return;
            e.printStackTrace ();
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

        private string formatTime (long ms) {
            return new System.Text.StringBuilder ()
                .Append ((ms / (1000 * 60 * 60)).ToString ("00")).Append (":")
                .Append ((ms / (1000 * 60) % 60).ToString ("00")).Append (":")
                .Append ((ms / 1000 % 60).ToString ("00")).ToString ();
        }

        #endregion

    }


}