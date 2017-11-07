namespace LiveRoku.Base{
    public interface IDownloadProgressBinder {
        void onDurationUpdate (long duration, string friendlyText);
        void onDownloadSizeUpdate (long totalSize, string friendlySize);
        void onBitRateUpdate (long bitRate, string bitRateText);
        void onMissionComplete(IMission mission);
    }
}
