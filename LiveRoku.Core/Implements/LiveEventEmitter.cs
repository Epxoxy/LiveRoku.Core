namespace LiveRoku.Core {
    using LiveRoku.Base;
    using LiveRoku.Base.Logger;
    using LiveRoku.Core.Models;
    using System.Threading.Tasks;
    using System;

    //Event boardcast part
    public partial class LiveFetchController : ILiveFetcher, ILogger {
        public ILowList<IDownloadProgressBinder> LiveProgressBinders => emitter.progressBinders;
        public ILowList<IStatusBinder> StatusBinders => emitter.statusBinders;
        public ILowList<IDanmakuResolver> DanmakuHandlers => emitter.danmakuHandlers;
        public ILowList<ILogHandler> LogHandlers => emitter.LogHandlers;
        public ILogger Logger => this;
        private LiveEventEmitter emitter = new LiveEventEmitter();

        public void log(Level level, string message) {
            emitter.log(level, message);
        }

        class LiveEventEmitter : IDanmakuResolver, ILogger {
            public ILowList<ILogHandler> LogHandlers => logHandlers;
            internal readonly LowList<IDownloadProgressBinder> progressBinders = new LowList<IDownloadProgressBinder>();
            internal readonly LowList<IStatusBinder> statusBinders = new LowList<IStatusBinder>();
            internal readonly LowList<IDanmakuResolver> danmakuHandlers = new LowList<IDanmakuResolver>();
            private readonly LowList<ILogHandler> logHandlers = new LowList<ILogHandler>();

            internal void emptyHandlers() {
                statusBinders.clear();
                danmakuHandlers.clear();
                progressBinders.clear();
                logHandlers.clear();
            }

            //..................
            //Boardcast events
            //Interface part below
            public void boardcastDurationUpdate(long duration, string friendlyText) {
                boardcast(progressBinders, binder => {
                    binder.onDurationUpdate(duration, friendlyText);
                });
            }
            public void boardcastDownloadSizeUpdate(long totalSize, string friendlySize) {
                boardcast(progressBinders, binder => {
                    binder.onDownloadSizeUpdate(totalSize, friendlySize);
                });
            }
            public void boardcastBitRateUpdate(long bitRate, string bitRateText) {
                boardcast(progressBinders, binder => {
                    binder.onBitRateUpdate(bitRate, bitRateText);
                });
            }
            public void boardcastMissionComplete(IMission mission) {
                boardcast(progressBinders, binder => {
                    binder.onMissionComplete(mission);
                });
            }
            public void onLiveStatusUpdate(bool isOn) {
                boardcast(danmakuHandlers, handler => {
                    handler.onLiveStatusUpdate(isOn);
                });
            }
            public void onHotUpdateByDanmaku(long popularity) {
                boardcast(danmakuHandlers, handler => {
                    handler.onHotUpdateByDanmaku(popularity);
                });
            }
            public void onDanmakuConnecting() {
                boardcast(danmakuHandlers, handler => {
                    handler.onDanmakuConnecting();
                });
            }
            public void onDanmakuActive() {
                boardcast(danmakuHandlers, handler => {
                    handler.onDanmakuActive();
                });
            }
            public void onDanmakuInactive() {
                boardcast(danmakuHandlers, handler => {
                    handler.onDanmakuInactive();
                });
            }
            public void onDanmakuReceive(DanmakuModel danmaku) {
                boardcast(danmakuHandlers, handler => {
                    handler.onDanmakuReceive(danmaku);
                });
            }
            public void boardcastPreparing() {
                boardcast(statusBinders, binder => {
                    binder.onPreparing();
                });
            }
            public void boardcastStreaming() {
                boardcast(statusBinders, binder => {
                    binder.onStreaming();
                });
            }
            public void boardcastWaiting() {
                boardcast(statusBinders, binder => {
                    binder.onWaiting();
                });
            }
            public void boardcastStopped() {
                boardcast(statusBinders, binder => {
                    binder.onStopped();
                });
            }

            public void log(Level level, string message) {
                boardcast(logHandlers, handler => {
                    handler.onLog(level, message);
                });
            }

            private void boardcast<T>(LowList<T> host, Action<T> action) where T : class {
                Task.Run(() => {
                    Parallel.ForEach(host, target => {
                        if (null != target) {
                            try { lock (target) action.Invoke(target); } catch (Exception e) {
                                var msg = $"boardcast error in {typeof(T).Name}.{action.Method.Name} : {e.ToString()}";
                                this.log(Level.Error, msg);
                                System.Diagnostics.Debug.WriteLine(msg, "Error");
                            }
                        }
                    });
                }).ContinueWith(task => {
                    task.Exception?.printOn(this);
                });
            }
        }
    }
}