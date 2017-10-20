namespace LiveRoku.Base {
    public interface IStatusBinder {
        void onPreparing ();
        void onStreaming ();
        void onWaiting ();
        void onStopped ();
    }
}