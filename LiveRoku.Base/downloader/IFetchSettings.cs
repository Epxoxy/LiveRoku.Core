namespace LiveRoku.Base{
    //Model interface for requesting download live video
    public interface IFetchSettings {
        string RoomId { get; }
        string Folder { get; }
        string FileFormat { get; }
        bool DownloadDanmaku { get; }
        bool AutoStart { get; }
    }
}
