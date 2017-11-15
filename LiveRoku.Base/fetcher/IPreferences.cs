namespace LiveRoku.Base{
    //Model interface for requesting download live video
    public interface IPreferences {
        //** Room id
        string ShortRoomId { get; }
        bool IsShortIdTheRealId { get; }
        //** Location
        string StoreFolder { get; }
        string StoreFileNameFormat { get; }
        //** Download control
        bool LocalDanmakuRequire { get; }
        bool LocalVideoRequire { get; }
        bool AutoStart { get; }
        //** Extra
        string UserAgent { get; }
        ISettingsBase Extra { get; }
    }
}
