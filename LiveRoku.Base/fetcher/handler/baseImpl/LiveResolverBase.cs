﻿using LiveRoku.Base.Logger;

namespace LiveRoku.Base {
    //All handlers implement base
    public class LiveResolverBase : ILiveResolver{

        public virtual void onPreparing(IContext ctx) { }
        public virtual void onStopped(IContext ctx) { }
        public virtual void onStreaming(IContext ctx) { }
        public virtual void onWaiting(IContext ctx) { }

        public virtual void onBitRateUpdate(long bitRate, string bitRateText) { }
        public virtual void onDownloadSizeUpdate(long totalSize, string friendlySize) { }
        public virtual void onDurationUpdate(long duration, string friendlyText) { }
        public virtual void onMissionComplete(IMission mission) { }

        public virtual void onDanmakuActive() { }
        public virtual void onDanmakuConnecting() { }
        public virtual void onDanmakuInactive() { }
        public virtual void onDanmakuReceive(DanmakuModel danmaku) { }
        public virtual void onHotUpdateByDanmaku(long popularity) { }
        public virtual void onLiveStatusUpdate(bool isOn) { }

        public virtual void onLog(Level level, string message) { }
    }
}
