namespace LiveRoku.Base {
    public enum LiveStatus { Preparing, Live, Round }
    public class RoomInfo {
        public LiveStatus LiveStatus { get; set; }
        public bool IsOn { get; set; }
        public string Title { get; set; }
        public int TimeLine { get; set; }
        public string Anchor { get; set; }

        public override string ToString () {
            return $"IsOn : {IsOn}, LiveStatus : {LiveStatus}, Title : {Title}";
        }
    }
}
