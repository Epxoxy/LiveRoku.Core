using LiveRoku.Base;
using System.Diagnostics;
using System.Threading.Tasks;

namespace LiveRoku.Core {
    internal class FetchBean {
        public string OriginRoomIdText { get; private set; }
        public string RealRoomIdText { get; private set; }
        public int OriginRoomId { get; private set; }
        public int RealRoomId { get; private set; }
        public string FlvAddress { get; private set; }
        public RoomInfo RoomInfo { get; private set; }
        public string Folder { get; set; }
        public string FileFullName { get; set; }
        public bool AutoStart { get; set; }
        public bool DanmakuNeed { get; set; }
        public ILogger Logger { get; set; }
        private readonly BiliApi biliApi;

        public FetchBean (int originRoomId, BiliApi biliApi) {
            this.OriginRoomId = originRoomId;
            this.OriginRoomIdText = originRoomId.ToString ();
            this.biliApi = biliApi;
        }

        public Task<bool> refresh () {
            return Task.Run (() => {
                var sw = new Stopwatch ();
                sw.Start ();
                Logger?.appendLine ("INFO", "-->sw--> start 00m:00s 000");
                //Try to get real roomId
                var realRoomIdTextTemp = biliApi.getRealRoomId (OriginRoomIdText);
                Logger?.appendLine ("INFO", $"-->sw--> fetched real roomId at {sw.Elapsed.ToString("mm'm:'ss's 'fff")}");
                int realRoomIdTemp;
                //Try to get flv url
                if (!string.IsNullOrEmpty (realRoomIdTextTemp) && int.TryParse (realRoomIdTextTemp, out realRoomIdTemp)) {
                    var flvUrl = biliApi.getRealUrl (realRoomIdTextTemp);
                    Logger?.appendLine ("INFO", $"-->sw--> fetched real url at {sw.Elapsed.ToString("mm'm:'ss's 'fff")}");
                    if (!string.IsNullOrEmpty (flvUrl)) {
                        this.RealRoomId = realRoomIdTemp;
                        this.RealRoomIdText = realRoomIdTextTemp;
                        this.FlvAddress = flvUrl;
                        this.RoomInfo = biliApi.getRoomInfo (RealRoomId);
                        Logger?.appendLine ("INFO", $"-->sw--> fetched room info at {sw.Elapsed.ToString("mm'm:'ss's 'fff")}");
                        return true;
                    }
                }
                return false;
            });
        }
    }
}
