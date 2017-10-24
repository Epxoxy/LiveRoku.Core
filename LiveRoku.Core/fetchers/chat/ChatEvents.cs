namespace LiveRoku.Core{
    using System;
    public delegate void HotUpdatedHandler(long amount);
    public delegate void DanmakuReceivedHandler(Base.DanmakuModel danmaku);
    public delegate void ConnectedHandler();
    public delegate void DisconnectedHandler(Exception e);
    public delegate void LogHandler(string msg);
}
