namespace LiveRoku.Core {
    using System.Threading.Tasks;
    using System;
    using Base.Logger;
    using Base;

    internal interface ILiveEventEmitter : ILiveProgressBinder, IStatusBinder {
        ILogger Logger { get; }
        void danmakuRecv (DanmakuModel danmaku);
    }
    public abstract class LiveEventEmitterBase : ILiveEventEmitter, ILogger {
        public ILowList<ILiveProgressBinder> LiveProgressBinders => progressBinders;
        public ILowList<IStatusBinder> StatusBinders => statusBinders;
        public ILowList<DanmakuResolver> DanmakuHandlers => danmakuHandlers;
        public ILowList<ILogHandler> LogHandlers => logHandlers;
        public ILogger Logger => this;

        private readonly LowList<ILiveProgressBinder> progressBinders = new LowList<ILiveProgressBinder> ();
        private readonly LowList<IStatusBinder> statusBinders = new LowList<IStatusBinder> ();
        private readonly LowList<DanmakuResolver> danmakuHandlers = new LowList<DanmakuResolver> ();
        private readonly LowList<ILogHandler> logHandlers = new LowList<ILogHandler> ();

        protected void emptyHandlers () {
            statusBinders.clear ();
            danmakuHandlers.clear ();
            progressBinders.clear ();
            logHandlers.clear();
        }

        protected virtual void onLiveStatusUpdateInternal (bool isOn) { }
        protected virtual void onStreamingInternal () { }
        protected virtual void onDanmakuRecvInternal (DanmakuModel danmaku) { }

        //..................
        //Boardcast events
        //Interface part below
        public void onStatusUpdate (bool isOn) {
            onLiveStatusUpdateInternal (isOn);
            boardcast (progressBinders, binder => {
                binder.onStatusUpdate (isOn);
            });
        }
        public void onDurationUpdate (long duration, string friendlyText) {
            boardcast (progressBinders, binder => {
                binder.onDurationUpdate (duration, friendlyText);
            });
        }
        public void onDownloadSizeUpdate (long totalSize, string friendlySize) {
            boardcast (progressBinders, binder => {
                binder.onDownloadSizeUpdate (totalSize, friendlySize);
            });
        }
        public void onBitRateUpdate (long bitRate, string bitRateText) {
            boardcast (progressBinders, binder => {
                binder.onBitRateUpdate (bitRate, bitRateText);
            });
        }
        public void onHotUpdate (long popularity) {
            boardcast (progressBinders, binder => {
                binder.onHotUpdate (popularity);
            });
        }
        public void onPreparing () {
            boardcast (statusBinders, binder => {
                binder.onPreparing ();
            });
        }
        public void onStreaming () {
            onStreamingInternal ();
            boardcast (statusBinders, binder => {
                binder.onStreaming ();
            });
        }
        public void onWaiting () {
            boardcast (statusBinders, binder => {
                binder.onWaiting ();
            });
        }
        public void onStopped () {
            boardcast (statusBinders, binder => {
                binder.onStopped ();
            });
        }
        public void danmakuRecv (DanmakuModel danmaku) {
            onDanmakuRecvInternal (danmaku);
            boardcast (danmakuHandlers, handler => {
                handler.Invoke (danmaku);
            });
        }
        public void onMissionComplete (IMission mission) {
            boardcast (progressBinders, binder => {
                binder.onMissionComplete (mission);
            });
        }
        public void log (Level level, string message) {
            boardcast (logHandlers, handler => {
                handler.onLog (level, message);
            });
        }

        private void boardcast<T> (LowList<T> host, Action<T> action) where T : class {
            Task.Run (() => {
                Parallel.ForEach (host, target => {
                    if (null != target) {
                        try { action.Invoke (target); }
                        catch (Exception e) {
                            var msg = $"boardcast error in {typeof (T).Name}.{action.Method.Name} : {e.Message}";
                            Logger?.log (Level.Error, msg);
                            System.Diagnostics.Debug.WriteLine (msg);
                        }
                    }
                });
            }).ContinueWith (task => {
                task.Exception?.printOn (Logger);
            });
        }
    }
}