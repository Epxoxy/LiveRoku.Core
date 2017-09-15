namespace LiveRoku.Core {
    public enum LiveStatus2 { Preparing, Live, Round }
    public class RoomInfo {
        public bool IsOn { get; set; }
        public LiveStatus2 LiveStatus { get; set; }
        public string Title { get; set; }
        public int TimeLine { get; set; }
        public string Anchor { get; set; }
        public override string ToString () {
            return $"IsOn : {IsOn}, LiveStatus : {LiveStatus}, Title : {Title}";
        }
    }
}
