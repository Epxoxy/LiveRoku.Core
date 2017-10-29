namespace LiveRoku.Core {
    internal class SimpleMission : Base.IMission {
        public string Subject { get; internal set; }
        public string VideoObjectName { get; internal set; }
        public string XMLObjectName { get; internal set; }
        public System.DateTime BeginTime { get; internal set; }
        public System.DateTime EndTime { get; internal set; }
        public long RecordSize { get; internal set; }
    }
}