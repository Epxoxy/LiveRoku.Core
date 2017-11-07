namespace LiveRoku.Base {
    public class DanmakuResolverBase : IDanmakuResolver {
        public virtual void onDanmakuActive() { }
        public virtual void onDanmakuConnecting() { }
        public virtual void onDanmakuInactive() { }
        public virtual void onDanmakuReceive(DanmakuModel danmaku) { }
        public virtual void onHotUpdateByDanmaku(long popularity) { }
        public virtual void onLiveStatusUpdateByDanmaku(bool isOn) { }
    }
}
