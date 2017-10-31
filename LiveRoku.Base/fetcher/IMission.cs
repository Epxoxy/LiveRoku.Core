namespace LiveRoku.Base {
    public interface IMission {
        System.Collections.Generic.List<IRoomInfo> RoomInfoHistory { get; }
        string VideoObjectName { get; }
        string XMLObjectName { get; }
        System.DateTime BeginTime { get; }
        System.DateTime EndTime { get; }
        long RecordSize { get; }
    }
}