using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LiveRoku.Base;
using LiveRoku.Base.Logger;
namespace LiveRoku.Core {
    internal class LiveDownloaderImpl {
        public bool IsStarted { get; private set; }
        public bool IsStreaming { get; private set; }
        private readonly FlvDownloader videoFetcher; //Download flv video
        private readonly DanmakuWriter dmWriter;
        private readonly ILiveEventEmitter em;
        private VideoInfo videoInfo;
        private SimpleMission record;
        private bool dmToLocalRequired = true;
        private CancellationTokenSource dmWritingSource;

        public LiveDownloaderImpl (ILiveEventEmitter em, string userAgent) {
            this.em = em;
            this.videoFetcher = new FlvDownloader (userAgent, null);
            this.dmWriter = new DanmakuWriter (Encoding.UTF8);
            this.videoFetcher.VideoInfoChecked = videoChecked;
            this.videoFetcher.IsRunningUpdated = downloadStatusUpdated;
            this.videoFetcher.OnDownloadCompleted = throwMission;
            this.videoFetcher.BytesReceived += downloadSizeUpdated;
        }

        public void reset () {
            stop (true);
            this.videoInfo = null;
            this.record = null;
            this.IsStarted = false;
            this.IsStreaming = false;
        }

        public Task download (string flvAddress, string fileFullName, bool dmRequired) {
            if (IsStarted) {
                return Task.FromResult (false);
            }
            IsStarted = true;
            this.dmToLocalRequired = dmRequired;
            videoInfo = new VideoInfo ();
            record = new SimpleMission ();
            record.VideoObjectName = fileFullName;
            record.XMLObjectName = Path.ChangeExtension (fileFullName, "xml");
            //Create FlvDloader and subscribe event handlers
            videoFetcher.BytesReceived -= onStreaming;
            videoFetcher.BytesReceived += onStreaming;
            videoFetcher.updateSavePath (fileFullName);
            return videoFetcher.startAsync (flvAddress);
        }

        public void stop (bool force) {
            IsStreaming = false;
            IsStarted = false;
            videoFetcher.BytesReceived -= onStreaming;
            videoFetcher.stop ();
            dmWriter.stop (force);
        }

        public void dispose () {
            videoFetcher.BytesReceived -= onStreaming;
            videoFetcher.BytesReceived -= downloadSizeUpdated;
            videoFetcher.VideoInfoChecked = null;
            videoFetcher.IsRunningUpdated = null;
        }

        public void danmakuToLocal (DanmakuModel danmaku) {
            if (IsStreaming && dmWriter?.IsRunning == true) {
                dmWriter.enqueue (danmaku);
            }
        }

        public void throwMission () {
            record.EndTime = DateTime.Now;
            var oldRecord = record;
            record = null;
            em.onMissionComplete (oldRecord);
        }

        private void downloadStatusUpdated (bool downloadRunning) {
            if (!downloadRunning) IsStreaming = false;
            em.Logger.log (Level.Info, $"Flv download {(downloadRunning ? "started" : "stopped")}.");
        }

        //Raise event when video info checked
        private void videoChecked (VideoInfo info) {
            var previous = this.videoInfo;
            this.videoInfo = info;
            if (previous.BitRate != info.BitRate) {
                em.Logger.log (Level.Info, $"{previous.BitRate} {info.BitRate}");
                var text = info.BitRate / 1000 + " Kbps";
                em.onBitRateUpdate (info.BitRate, text);
            }
            if (previous.Duration != info.Duration) {
                var text = SharedHelper.getFriendlyTime (info.Duration);
                em.onDurationUpdate (info.Duration, text);
            }
        }

        private void onStreaming (long bytes) {
            if (bytes < 2 || IsStreaming) return;
            IsStreaming = true;
            record.BeginTime = DateTime.Now;
            videoFetcher.BytesReceived -= onStreaming;
            em.Logger.log (Level.Info, "Streaming check.....");
            if (dmWritingSource?.Token.CanBeCanceled == true) {
                dmWritingSource.Cancel ();
            }
            dmWritingSource = new CancellationTokenSource ();
            Task.Run (async () => {
                dmWriter.stop (force : true);
                if (!dmToLocalRequired) return;
                await activeWriteDanmaku ();
            }, dmWritingSource.Token);
            em.onStreaming ();
        }

        private void downloadSizeUpdated (long totalBytes) {
            record.RecordSize = totalBytes;
            //OnDownloadSizeUpdate
            var text = totalBytes.ToFileSize ();
            em.onDownloadSizeUpdate (totalBytes, text);
        }

        private Task activeWriteDanmaku () {
            var startTimestamp = Convert.ToInt64 (DateTime.UtcNow.totalMsToGreenTime ());
            em.Logger.log (Level.Info, "Start danmaku storage.....");
            return dmWriter.startAsync (record.XMLObjectName, startTimestamp);
        }
    }
}