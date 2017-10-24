namespace LiveRoku.Base {
    public enum LiveStatus { Preparing, Live, Round }
    public interface IRoomInfo {
        LiveStatus LiveStatus { get; }
        bool IsOn { get; }
        string Title { get; }
        int TimeLine { get; }
        string Anchor { get; }
        string RawData { get; }
    }
}