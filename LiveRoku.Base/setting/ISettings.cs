namespace LiveRoku.Base {
    public interface ISettings : ISettingsBase{
        bool CanSaveToDisk { get; }
        bool contains (string key);
        void clear ();
        bool saveToDisk();
    }
}
