namespace LiveRoku.Core.Models {
    internal class SimpleMission : Base.IMission {
        public System.Collections.Generic.List<Base.IRoomInfo> RoomInfoHistory { get; internal set; }
        public string VideoObjectName { get; internal set; }
        public string XMLObjectName { get; internal set; }
        public System.DateTime BeginTime { get; internal set; }
        public System.DateTime EndTime { get; internal set; }
        public long RecordSize { get; internal set; }
    }
}