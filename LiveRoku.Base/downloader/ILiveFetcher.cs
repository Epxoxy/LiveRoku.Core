using System;
using System.Collections.Generic;
namespace LiveRoku.Base {
    //To implements ILiveFetcher must have a default constructor as
    //public Constructor (IRequestModel model, string userAgent, int requestTimeout);
    public interface ILiveFetcher : IDownloader, IDisposable {
        LowList<ILiveProgressBinder> LiveProgressBinders { get; }
        LowList<IStatusBinder> StatusBinders { get; }
        LowList<DanmakuResolver> DanmakuHandlers { get; }
        ILogger Logger { get; }
        bool IsStreaming { get; }

        RoomInfo fetchRoomInfo(bool refresh);
        //For extension
        void bindState(string key, Action<Dictionary<string, object>> doWhat);
        void putExtra(string key, object value);
        object getExtra(string key);
    }
}