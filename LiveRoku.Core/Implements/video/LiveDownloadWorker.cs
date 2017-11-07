namespace LiveRoku.Core {
    using LiveRoku.Base;
    using LiveRoku.Base.Logger;
    using LiveRoku.Core.Models;
    using System;
    using System.IO;
    using System.Text;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Collections.Generic;
    using LiveRoku.Core.Common.Media;

    internal class LiveDownloadWorker : FlvDownloader {
        public bool IsStarted { get; private set; }
        public bool IsStreaming { get; private set; }
        public Action<long> BitRateUpdated { get; set; }
        public Action<long> DurationUpdated { get; set; }
        public Action<long> DownloadSizeUpdated { get; set; }
        public Action<IMission> MissionCompleted { get; set; }
        public Action Streaming { get; set; }
        //private readonly
        private DanmakuWriter dmWriter;
        private readonly ILogger logger;
        private VideoInfo videoInfo;
        private SimpleMission record;
        private bool dmToLocalRequired = true;
        private Action<long> streamingCheck = delegate { };

        public LiveDownloadWorker (ILogger logger, string userAgent) : base(userAgent, null) {
            this.logger = logger;
        }

        public void reset () {
            stopAsync (true);
            this.videoInfo = null;
            this.record = null;
            this.IsStarted = false;
            this.IsStreaming = false;
        }

        public Task<bool> downloadAsync (string flvAddress, string fileFullName, bool dmRequired) {
            if (IsStarted) {
                return Task.FromResult(false);
            }
            IsStarted = true;
            this.dmToLocalRequired = dmRequired;
            videoInfo = new VideoInfo ();
            record = new SimpleMission { RoomInfoHistory = new List<IRoomInfo>()};
            record.BeginTime = DateTime.Now;
            record.VideoObjectName = fileFullName;
            record.XMLObjectName = Path.ChangeExtension (fileFullName, "xml");
            //Create FlvDloader and subscribe event handlers
            streamingCheck = onStreaming;
            this.updateSavePath (fileFullName);
            dmWriter = new DanmakuWriter(Encoding.UTF8);
            return this.startAsync (flvAddress);
        }

        public void stopAsync (bool force) {
            IsStreaming = false;
            IsStarted = false;
            streamingCheck = delegate { };
            this.stopAsync ();
            dmWriter?.stop (force);
        }
        
        //Enqueue by outside
        public void danmakuToLocal (DanmakuModel danmaku) {
            if (IsStreaming && dmWriter?.IsRunning == true) {
                dmWriter.enqueue (danmaku);
            }
        }

        //Update by outside
        public void addRoomInfo(IRoomInfo info) {
            if (record == null || info == null) return;
            if (info.Equals(record.RoomInfoHistory.LastOrDefault())) return;
            record.RoomInfoHistory.Add(info);
        }

        protected override void onDownloadEnded() {
            base.onDownloadEnded();
            IsStarted = false;
            IsStreaming = false;
            if(record!= null) {
                record.EndTime = DateTime.Now;
                var oldRecord = record;
                record = null;
                MissionCompleted?.Invoke(oldRecord);
            }
        }

        protected override void onIsRunningUpdated(bool downloadRunning) {
            base.onIsRunningUpdated(downloadRunning);
            if (!downloadRunning) IsStreaming = false;
            logger.log (Level.Info, $"Video download is {(downloadRunning ? "starting" : "stopped")}.");
        }

        //Raise event when video info checked
        protected override void onVideoInfoChecked(VideoInfo info) {
            base.onVideoInfoChecked(info);
            var previous = this.videoInfo;
            this.videoInfo = info;
            if (previous.BitRate != info.BitRate) {
                BitRateUpdated?.Invoke(info.BitRate);
            }
            if (previous.Duration != info.Duration) {
                DurationUpdated?.Invoke(info.Duration);
            }
        }

        private void onStreaming (long bytes) {
            if (bytes < 2 || IsStreaming) return;
            IsStreaming = true;
            streamingCheck = delegate { };
            record.BeginTime = DateTime.Now;
            System.Diagnostics.Debug.WriteLine("Streaming check.....", "worker");
            dmWriter.stop(force: true);
            if (dmToLocalRequired) {
                activeWriteDanmaku();
            }
            Streaming?.Invoke();
        }

        protected override void onBytesReceived(long totalBytes) {
            base.onBytesReceived(totalBytes);
            record.RecordSize = totalBytes;
            streamingCheck.Invoke(totalBytes);
            //OnDownloadSizeUpdate
            DownloadSizeUpdated?.Invoke(totalBytes);
        }

        protected override VideoInfo getVideoInfo(string path, long bytesReceived) {
            var mediaLib = new MediaInfo();
            //Get basic parameters
            mediaLib.Open(path);
            var durationText = mediaLib.Get(StreamKind.General, 0, "Duration");
            var videoBrText = mediaLib.Get(StreamKind.Video, 0, "BitRate");
            var audioBrText = mediaLib.Get(StreamKind.Audio, 0, "BitRate");
            mediaLib.Close();
            //Parse basic parameters
            int videoBr = 0, audioBr = 0, duration = 0;
            int.TryParse(videoBrText, out videoBr);
            int.TryParse(audioBrText, out audioBr);
            int.TryParse(durationText, out duration);
            return new VideoInfo {
                BitRate = videoBr + audioBr,
                Duration = duration,
                Bytes = bytesReceived
            };
        }

        private Task activeWriteDanmaku () {
            var startTimestamp = Convert.ToInt64 (DateTime.UtcNow.totalMsToGreenTime ());
            System.Diagnostics.Debug.WriteLine("Active danmaku writer.....", "worker");
            return dmWriter.startAsync (record.XMLObjectName, startTimestamp);
        }
    }
}