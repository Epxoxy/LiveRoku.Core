namespace LiveRoku.Base {
    public class DownloadProgressBinderBase : IDownloadProgressBinder {
        public virtual void onBitRateUpdate (long bitRate, string bitRateText) { }
        public virtual void onDownloadSizeUpdate (long totalSize, string friendlySize) { }
        public virtual void onDurationUpdate (long duration, string friendlyText) { }
        public virtual void onMissionComplete(IMission mission) { }
    }
}