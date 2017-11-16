namespace LiveRoku.Core {
    using LiveRoku.Base;
    using LiveRoku.Base.Logger;
    using LiveRoku.Core.Common;
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    internal interface IDownloadActor {
        bool IsStreaming { get; }
        void onLiveCommand(MsgTypeEnum type, PreferencesLite pref, RoomDataLiteApi remote);
        void onDanmaku(DanmakuModel dm);
        void onRoomInfo(IRoomInfo info);
        void onCallDownload(PreferencesLite pref, RoomDataLiteApi remote);
        void onReset();
        void stopAsync(bool force);
    }

    internal class EmptyDownloadActor : IDownloadActor {
        public virtual bool IsStreaming => false;
        public virtual void onCallDownload(PreferencesLite pref, RoomDataLiteApi dataApi) { }
        public virtual void onDanmaku(DanmakuModel dm) { }
        public virtual void onLiveCommand(MsgTypeEnum type, PreferencesLite pref, RoomDataLiteApi argsBean) { }
        public virtual void onRoomInfo(IRoomInfo info) { }
        public virtual void onReset() { }
        public virtual void stopAsync(bool force) { }
    }
    
    internal class VideoDownloadActor : EmptyDownloadActor {
        public override bool IsStreaming => worker.IsStreaming;
        private readonly LatestAwaitable reinvoke = new LatestAwaitable();
        private readonly CancellationManager cancelMgr;
        private readonly LiveDownloadWorker worker;
        private readonly int requestTimeout = 10000;
        private readonly Func<bool> isWorkModeAndLiveOn;
        private readonly ILogger logger;

        public VideoDownloadActor(LiveDownloadWorker worker, Func<bool> isWorkModeAndLiveOn, int timeout, CancellationManager cancelMgr, ILogger logger) {
            this.worker = worker;
            this.isWorkModeAndLiveOn = isWorkModeAndLiveOn;
            this.requestTimeout = timeout;
            this.logger = logger;
            this.cancelMgr = cancelMgr;
        }

        public override void onCallDownload(PreferencesLite pref, RoomDataLiteApi remote) => downloadAsyncBy(worker, pref, remote);
        public override void onDanmaku(DanmakuModel dm) => worker.danmakuToLocal(dm);
        public override void onLiveCommand(MsgTypeEnum type, PreferencesLite pref, RoomDataLiteApi remote) => confirmDownloadWorker(type, pref, remote);
        public override void onRoomInfo(IRoomInfo info) => worker.addRoomInfo(info);
        public override void onReset() => worker.reset();
        public override void stopAsync(bool force) => worker.stopAsync(force);

        private Task<bool> downloadAsyncBy(LiveDownloadWorker downloader, PreferencesLite pref, RoomDataLiteApi dataApi) {
            //All ready, start now
            var fileName = getFileFullName(pref.FileNameFormat, pref.Folder, dataApi.RealRoomId, DateTime.Now);
            if (pref.VideoRequire) {
                logger.log(Level.Info, $"Download ready, target: {dataApi.VideoUrl}");
                return downloader.downloadAsync(dataApi.VideoUrl, fileName, pref.DanmakuRequire);
            } else {
                return Task.FromResult(false);
            }
        }
        
        private void confirmDownloadWorker(MsgTypeEnum type, PreferencesLite pref, RoomDataLiteApi dataApi) {
            if (!pref.VideoRequire)
                return;
            if (type == MsgTypeEnum.LiveEnd) {
                reinvoke.release();
                cancelMgr.cancel(nameof(confirmDownloadWorker));
                cancelMgr.remove(nameof(confirmDownloadWorker));
                Debug.WriteLine("Trying to stop downloader because live end.", "tasks");
                worker.stopAsync(false);
            } else if (pref.AutoStart && reinvoke.addAndRegister()) {
                //Just need to know if there has newest request
                //Keep one enter, sometimes LiveStart msg will send over one time
                //TODO Test code
                Action invokeLoop = null;
                invokeLoop = () => {
                    Debug.WriteLine($"1/{reinvoke.RequestTimes} Register ok. Now invoking", "re-work");
                    activeWorker(reinvoke, pref, dataApi).ContinueWith(task => {
                        Debug.WriteLine($"Restart downloader Completed.IsStreaming -{worker.IsStreaming}", "re-work");
                        if (reinvoke.unregisterAndReRegister()) {
                            Debug.WriteLine($"Continue remain {reinvoke.RequestTimes}", "re-work");
                            invokeLoop.Invoke();
                        }
                    });
                };
                invokeLoop.Invoke();
            }
        }

        private Task activeWorker(LatestAwaitable what, PreferencesLite pref, RoomDataLiteApi dataApi) {
            return cancelMgr.runOnlyOne(nameof(confirmDownloadWorker), token => {
                try {
                    if (!isRestartDownloadAllow()) {
                        Debug.WriteLine("Restart not allow.", "re-work");
                    } else if (!dataApi.fetchRealIdAndUrl("!MgdId-" + Thread.CurrentThread.ManagedThreadId)/*Wait re-get data*/) {
                        Debug.WriteLine("Restart fail --> fetch url/id.", "re-work");
                    } else if (token.IsCancellationRequested) {
                        //Confirm task cancelled
                        Debug.WriteLine("Cancellation requested.", "re-work");
                    } else if (isRestartDownloadAllow()) {
                        //Reconfirm download required and not start 
                        Debug.WriteLine($"1/{what.RequestTimes} Trying to restart downloader.", "re-work");
                        logger.log(Level.Info, $"Flv address updated : {dataApi.VideoUrl}");
                        downloadAsyncBy(worker, pref, dataApi);
                        Thread.Sleep(6000);//Wait downloader streaming
                        if (!worker.IsStreaming) {
                            worker.stopAsync(true);
                            Debug.WriteLine("Timeout ok, but start fail.", "re-work");
                        }
                        Debug.WriteLine("Sleep out", "tasks");
                    } else /*Reconfirm but restart not allow*/{
                        Debug.WriteLine("Reconfirm restart not allow.", "re-work");
                    }
                    Thread.Sleep(10);//reduce CPU usage calls frequently
                } catch (Exception e) {
                    e.printOn(logger);
                }
            }, requestTimeout, () => /*If task cancelled*/{
                //TODO Maybe should stop when timeout if download not start
                Debug.WriteLine($"Restart downloader cancelled.IsStreaming -{worker.IsStreaming}", "re-work");
            });
        }

        private bool isRestartDownloadAllow() {
            //For test --> return false;
            return isWorkModeAndLiveOn() && !worker.IsStreaming;
        }

        private string getFileFullName(string format, string folder, string realRoomId, DateTime baseTime) {
            var fileName = string.Empty;
            try {
                fileName = format.Replace("{roomId}", realRoomId.ToString())
                    .Replace("{Y}", baseTime.Year.ToString("D4"))
                    .Replace("{M}", baseTime.Month.ToString("D2"))
                    .Replace("{d}", baseTime.Day.ToString("D2"))
                    .Replace("{H}", baseTime.Hour.ToString("D2"))
                    .Replace("{m}", baseTime.Minute.ToString("D2"))
                    .Replace("{s}", baseTime.Second.ToString("D2"));
            } catch (Exception e) {
                e.printStackTrace();
                fileName = $"{realRoomId}-{baseTime.ToString("yyyy-MM-dd-HH-mm-ss")}";
            }
            return System.IO.Path.Combine(folder, fileName);
        }
        
    }
}
