namespace LiveRoku.Base{
    //Model interface for requesting download live video
    public interface IPreferences {
        //** Room id
        string ShortRoomId { get; }
        bool IsShortIdTheRealId { get; }
        //** Location
        string Folder { get; }
        string FileFormat { get; }
        //** Download control
        bool DanmakuRequire { get; }
        bool VideoRequire { get; }
        bool AutoStart { get; }
        //** Extra
        string UserAgent { get; }
        ISettingsBase Extra { get; }
    }
}
