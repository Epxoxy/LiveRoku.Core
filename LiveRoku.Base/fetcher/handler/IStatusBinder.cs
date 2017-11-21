namespace LiveRoku.Base {
    public interface IStatusBinder {
        void onPreparing (IContext ctx);
        void onStreaming (IContext ctx);
        void onWaiting (IContext ctx);
        void onStopped (IContext ctx);
    }
}