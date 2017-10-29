namespace LiveRoku.Base {
    public class StatusAndLiveProgressBinderBase : ILiveProgressBinder,IStatusBinder {

        public virtual void onBitRateUpdate (long bitRate, string bitRateText) { }
        public virtual void onDownloadSizeUpdate (long totalSize, string friendlySize) { }
        public virtual void onDurationUpdate (long duration, string friendlyText) { }
        public virtual void onHotUpdate (long popularity) { }
        public virtual void onStatusUpdate (bool isOn) { }
        public virtual void onMissionComplete(IMission mission) { }

        public virtual void onPreparing() { }
        public virtual void onStopped() { }
        public virtual void onStreaming() { }
        public virtual void onWaiting() { }
    }
}