namespace LiveRoku.Core.Models {
    using LiveRoku.Base;
    public interface IWebApi {

        bool tryGetValidDmServerBean(string realRoomId, out ServerData data);
        bool tryGetRoomIdAndUrl(string shortRoomId, out string realRoomId, out string flvUrl);
        bool tryGetVideoUrl(string realRoomId, out string videoUrl);

        ServerData getDmServerData(string roomId, bool useDefaultOnException = false);
        string getRealRoomId(string shortRoomId);
        string getVideoUrl(string realRoomId);
        IRoomInfo getRoomInfo(string realRoomId);

    }
}
