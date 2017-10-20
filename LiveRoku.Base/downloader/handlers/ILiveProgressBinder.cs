namespace LiveRoku.Base{
    public interface ILiveProgressBinder {
        void onStatusUpdate (bool isOn);
        void onDurationUpdate (long duration, string friendlyText);
        void onDownloadSizeUpdate (long totalSize, string friendlySize);
        void onBitRateUpdate (long bitRate, string bitRateText);
        void onHotUpdate (long popularity);
        void onMissionComplete(IMission mission);
    }
}
