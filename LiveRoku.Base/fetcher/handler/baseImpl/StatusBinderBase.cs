namespace LiveRoku.Base {
    public class StatusBinderBase : IStatusBinder {
        public virtual void onPreparing (IContext ctx) { }
        public virtual void onStopped (IContext ctx) { }
        public virtual void onStreaming (IContext ctx) { }
        public virtual void onWaiting (IContext ctx) { }
    }
}