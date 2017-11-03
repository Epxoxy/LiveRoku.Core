namespace LiveRoku.Core.Models {
    internal class RoomInfo : Base.IRoomInfo {
        public Base.LiveStatus LiveStatus { get; internal set; }
        public bool IsOn { get; internal set; }
        public string Title { get; internal set; }
        public int TimeLine { get; internal set; }
        public string Anchor { get; internal set; }
        public string RawData { get; private set; }

        public RoomInfo(string rawData) {
            this.RawData = rawData;
        }
        
        public override string ToString () {
            return RawData;
        }

        public override bool Equals(object obj) {
            if (obj == null)
                return false;
            var info = obj as RoomInfo;
            if (info == null)
                return false;
            if (string.IsNullOrEmpty(info.RawData))
                return string.IsNullOrEmpty(info.RawData);
            //All members comes from RawData
            return info.RawData.Equals(info.RawData);
        }

        public override int GetHashCode() {
            return 31 * 17 + ((RawData == null) ? -1 : RawData.GetHashCode());
        }
    }
}