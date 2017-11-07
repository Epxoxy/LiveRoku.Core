namespace LiveRoku.Base{
    public interface IDanmakuResolver {
        void onDanmakuConnecting();
        void onDanmakuActive();
        void onDanmakuInactive();
        void onDanmakuReceive(DanmakuModel danmaku);
        void onHotUpdateByDanmaku(long popularity);
        void onLiveStatusUpdateByDanmaku(bool isOn);
    }
}
