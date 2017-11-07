namespace LiveRoku.Base {
    public class StatusBinderBase : IStatusBinder {
        public virtual void onPreparing () { }
        public virtual void onStopped () { }
        public virtual void onStreaming () { }
        public virtual void onWaiting () { }
    }
}