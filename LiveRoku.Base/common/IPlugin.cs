namespace LiveRoku.Base {
    public interface IPlugin {
        string Name { get; }
        string Description { get; }
        void onInitialize(IStorage storage);
        void onAttach (ILiveFetcher fetcher);
        void onDetach ();
    }
}