namespace LiveRoku.Base {
    public interface IMission {
        string Subject { get; }
        string VideoObjectName { get; }
        string XMLObjectName { get; }
        System.DateTime BeginTime { get; }
        System.DateTime EndTime { get; }
        long RecordSize { get; }
    }
}