namespace LiveRoku.Base {
    public interface IDownloader {
        bool IsRunning { get; }
        void start (string url = null);
        void stop (bool force = false);
    }
}