namespace LiveRoku.Base {
    public class LiveProgressBinderBase : ILiveProgressBinder {
        public virtual void onBitRateUpdate (long bitRate, string bitRateText) { }
        public virtual void onDownloadSizeUpdate (long totalSize, string friendlySize) { }
        public virtual void onDurationUpdate (long duration, string friendlyText) { }
        public virtual void onHotUpdate (long popularity) { }
        public virtual void onStatusUpdate (bool isOn) { }
        public virtual void onMissionComplete(IMission mission) { }
    }
}