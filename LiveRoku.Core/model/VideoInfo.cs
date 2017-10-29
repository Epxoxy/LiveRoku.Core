namespace LiveRoku.Core {
    public class VideoInfo {
        public long Bytes { get; internal set; }
        public long BitRate { get; internal set; }
        public long Duration { get; internal set; }

        public VideoInfo () { }

        public VideoInfo (long bytes, long bitRate, long duration) {
            this.Bytes = bytes;
            this.BitRate = bitRate;
            this.Duration = duration;
        }
    }
}