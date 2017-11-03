﻿namespace LiveRoku.Core {
    using System;
    using System.IO;
    using System.Text;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Collections.Generic;
    using LiveRoku.Base;
    using LiveRoku.Base.Logger;
    using LiveRoku.Core.Models;

    internal class LiveDownloadWorker {
        public bool IsStarted { get; private set; }
        public bool IsStreaming { get; private set; }
        public Action<long> BitRateUpdated { get; set; }
        public Action<long> DurationUpdated { get; set; }
        public Action<long> DownloadSizeUpdated { get; set; }
        public Action<IMission> MissionCompleted { get; set; }
        public Action OnStreaming { get; set; }
        //private readonly
        private readonly FlvDownloader videoFetcher; //Download flv video
        private DanmakuWriter dmWriter;
        private readonly ILogger logger;
        private VideoInfo videoInfo;
        private SimpleMission record;
        private bool dmToLocalRequired = true;
        private CancellationTokenSource dmWritingCTS;

        public LiveDownloadWorker (ILogger logger, string userAgent) {
            this.logger = logger;
            this.videoFetcher = new FlvDownloader (userAgent, null);
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
            record = new SimpleMission { RoomInfoHistory = new List<IRoomInfo>()};
            record.BeginTime = DateTime.Now;
            record.VideoObjectName = fileFullName;
            record.XMLObjectName = Path.ChangeExtension (fileFullName, "xml");
            //Create FlvDloader and subscribe event handlers
            videoFetcher.BytesReceived -= onStreaming;
            videoFetcher.BytesReceived += onStreaming;
            videoFetcher.updateSavePath (fileFullName);
            dmWriter = new DanmakuWriter(Encoding.UTF8);
            return videoFetcher.startAsync (flvAddress);
        }

        public void stop (bool force) {
            IsStreaming = false;
            IsStarted = false;
            videoFetcher.BytesReceived -= onStreaming;
            videoFetcher.stop ();
            dmWriter?.stop (force);
        }

        public void dispose () {
            videoFetcher.BytesReceived -= onStreaming;
            videoFetcher.BytesReceived -= downloadSizeUpdated;
            videoFetcher.VideoInfoChecked = null;
            videoFetcher.IsRunningUpdated = null;
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

        private void throwMission () {
            record.EndTime = DateTime.Now;
            var oldRecord = record;
            record = null;
            MissionCompleted?.Invoke(oldRecord);
        }

        private void downloadStatusUpdated (bool downloadRunning) {
            if (!downloadRunning) IsStreaming = false;
            logger.log (Level.Info, $"Flv download {(downloadRunning ? "started" : "stopped")}.");
        }

        //Raise event when video info checked
        private void videoChecked (VideoInfo info) {
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
            record.BeginTime = DateTime.Now;
            videoFetcher.BytesReceived -= onStreaming;
            logger.log (Level.Info, "Streaming check.....");
            if (dmWritingCTS?.Token.CanBeCanceled == true) {
                dmWritingCTS.Cancel ();
            }
            dmWritingCTS = new CancellationTokenSource ();
            Task.Run (async () => {
                dmWriter.stop (force : true);
                if (!dmToLocalRequired) return;
                await activeWriteDanmaku ();
            }, dmWritingCTS.Token);
            OnStreaming?.Invoke();
        }

        private void downloadSizeUpdated (long totalBytes) {
            record.RecordSize = totalBytes;
            //OnDownloadSizeUpdate
            DownloadSizeUpdated?.Invoke(totalBytes);
        }

        private Task activeWriteDanmaku () {
            var startTimestamp = Convert.ToInt64 (DateTime.UtcNow.totalMsToGreenTime ());
            logger.log (Level.Info, "Start danmaku storage.....");
            return dmWriter.startAsync (record.XMLObjectName, startTimestamp);
        }
    }
}