namespace LiveRoku.Base {
    using System;
    public class DanmakuOptions {
        public int DmType { get; set; } = 1;
        public int Fontsize { get; set; }
        public int Color { get; set; }
        public string UserHash { get; set; }
        public long SendTimestamp { get; set; }
        public long CreateTime { get; internal set; }
        public string CommentText { get; set; }

        public DanmakuOptions(long createTime) {
            this.CreateTime = createTime;
        }

        public override string ToString() {
            return ToString(0);
        }

        public string ToString(long startTime) {
            var alignTime = Convert.ToDouble(CreateTime - startTime) / 1000;
            return $"<d p=\"{alignTime},{DmType},{Fontsize},{Color},{SendTimestamp},0,{UserHash},0\">{CommentText}</d>";
        }
    }
}
