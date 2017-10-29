namespace LiveRoku.Core {
    using System;
    using System.Net;
    using System.Threading.Tasks;
    public delegate void BytesReceived (long totalBytes);
    internal class FlvDownloader : FileDownloaderBase {

        public event BytesReceived BytesReceived;
        public Action<VideoInfo> VideoInfoChecked;
        public Action<bool> IsRunningUpdated;
        public VideoInfo LastestVideoCheckInfo { get; private set; }
        private readonly long increment = 300000;
        private string userAgent;
        private long millsToCheck;
        private long sizeToCheck;

        public FlvDownloader (string userAgent, string savePath, int checkInterval = 300000) : base (savePath) {
            this.increment = checkInterval;
            this.userAgent = userAgent;
        }

        protected override void onStarting () {
            sizeToCheck = increment;
            errorTimes = 0;
            IsRunningUpdated?.Invoke (true);
        }

        protected override void onStopped () {
            IsRunningUpdated?.Invoke (false);
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
                            info = updateFlvInfo (base.savePath, e.BytesReceived);
                            LastestVideoCheckInfo = info;
                        } catch (Exception ex) {
                            ex.printStackTrace ();
                            ++errorTimes;
                        }
                        if (info != null) {
                            VideoInfoChecked?.Invoke (info);
                        }
                    }).ContinueWith (task => { task.Exception?.printStackTrace (); });
                }
            }
            BytesReceived?.Invoke (e.BytesReceived);
        }

        private VideoInfo updateFlvInfo (string path, long bytesReceived) {
            var mediaLib = new MediaInfo ();
            //Get basic parameters
            mediaLib.Open (path);
            var durationText = mediaLib.Get (StreamKind.General, 0, "Duration");
            var videoBrText = mediaLib.Get (StreamKind.Video, 0, "BitRate");
            var audioBrText = mediaLib.Get (StreamKind.Audio, 0, "BitRate");
            mediaLib.Close ();
            //Parse basic parameters
            int videoBr = 0, audioBr = 0, duration = 0;
            int.TryParse (videoBrText, out videoBr);
            int.TryParse (audioBrText, out audioBr);
            int.TryParse (durationText, out duration);
            return new VideoInfo {
                BitRate = videoBr + audioBr,
                Duration = duration,
                Bytes = bytesReceived
            };
        }

        //For preventing some errors case by media.dll
        private int errorTimes;
    }

}