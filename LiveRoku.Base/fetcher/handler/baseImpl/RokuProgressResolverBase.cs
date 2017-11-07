namespace LiveRoku.Base {
    //Resolve event on roku
    class RokuProgressResolverBase : IDownloadProgressBinder, IDanmakuResolver{
        public virtual void onBitRateUpdate(long bitRate, string bitRateText) { }
        public virtual void onDownloadSizeUpdate(long totalSize, string friendlySize) { }
        public virtual void onDurationUpdate(long duration, string friendlyText) { }
        public virtual void onMissionComplete(IMission mission) { }

        public virtual void onDanmakuActive() { }
        public virtual void onDanmakuConnecting() { }
        public virtual void onDanmakuInactive() { }
        public virtual void onDanmakuReceive(DanmakuModel danmaku) { }
        public virtual void onHotUpdateByDanmaku(long popularity) { }
        public virtual void onLiveStatusUpdateByDanmaku(bool isOn) { }
    }
}
