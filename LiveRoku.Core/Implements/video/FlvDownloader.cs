namespace LiveRoku.Core.Download {
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using LiveRoku.Core.Models;
    internal abstract class FlvDownloader : FileDownloaderBase {
        
        public VideoInfo LastestVideoCheckInfo { get; private set; }
        private readonly long increment = 300000;
        private string userAgent;
        private long millsToCheck;
        private long sizeToCheck;

        public FlvDownloader (string userAgent, string savePath, int checkInterval = 300000) : base (savePath) {
            this.increment = checkInterval;
            this.userAgent = userAgent;
        }

        protected virtual void onBytesReceived(long bytesReceived) { }
        protected virtual void onVideoInfoChecked(VideoInfo info) { }
        protected virtual void onIsRunningUpdated(bool isRunning) { }
        protected override void onDownloadEnded() { }


        protected override void onStarting () {
            sizeToCheck = increment;
            errorTimes = 0;
            onIsRunningUpdated (true);
        }

        protected override void onStopped () {
            onIsRunningUpdated (false);
        }

        protected override void initClient (WebClient client) {
            client.Headers.Add ("Accept: */*");
            client.Headers.Add ("User-Agent: " + userAgent);
            client.Headers.Add ("Accept-Language: zh-CN,zh;q=0.8,en;q=0.6,ja;q=0.4");
        }

        protected override void onProgressUpdate (DownloadProgressChangedEventArgs e) {
            if (e.BytesReceived >= sizeToCheck && errorTimes < 5) {
                sizeToCheck += increment;
                long current = (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond);
                if (millsToCheck <= current) {
                    millsToCheck = current + 1000;
                    Task.Run (() => {
                        VideoInfo info = null;
                        try {
                            info = getVideoInfo (base.savePath, e.BytesReceived);
                            LastestVideoCheckInfo = info;
                        } catch (Exception ex) {
                            ex.printStackTrace ();
                            ++errorTimes;
                        }
                        if (info != null) {
                            onVideoInfoChecked (info);
                        }
                    }).ContinueWith (task => { task.Exception?.printStackTrace (); });
                }
            }
            onBytesReceived (e.BytesReceived);
        }

        protected abstract VideoInfo getVideoInfo(string path, long bytesReceived);

        //For preventing some errors case by media.dll
        private int errorTimes;
    }

}