namespace LiveRoku.Base {
    public interface IContext {
        ILiveFetcher Fetcher { get; }
        IPreferences Preferences { get; }
        ISettingsBase RuntimeExtra { get; }
    }
}
