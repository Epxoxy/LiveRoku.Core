using System;
namespace LiveRoku.Base {
    //To implements ILiveFetcher must have a default constructor as
    //public Constructor (IRequestModel model, string userAgent, int requestTimeout);
    public interface ILiveFetcher : IDisposable {
        ILowList<IDownloadProgressBinder> LiveProgressBinders { get; }
        ILowList<IStatusBinder> StatusBinders { get; }
        ILowList<IDanmakuResolver> DanmakuHandlers { get; }
        ISettingsBase Extra { get; }
        Logger.ILogger Logger { get; }
        bool IsStreaming { get; }
        bool IsRunning { get; }

        //Method
        void start ();
        void stop (bool force = false);
        IRoomInfo getRoomInfo(bool refresh);
    }
}