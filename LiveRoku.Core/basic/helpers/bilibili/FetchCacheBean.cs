using LiveRoku.Base;
using System.Diagnostics;
using System.Threading.Tasks;

namespace LiveRoku.Core {
    internal class FetchCacheBean {
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
        private object fetchLocker = new object();

        public FetchCacheBean(int originRoomId, BiliApi biliApi, ILogger logger) {
            this.OriginRoomId = originRoomId;
            this.OriginRoomIdText = originRoomId.ToString ();
            this.biliApi = biliApi;
            this.Logger = logger;
        }

        public FetchCacheBean (int originRoomId, BiliApi biliApi) : this(originRoomId, biliApi, null) {
        }

        public Task<bool> refreshAllAsync () {
            return Task.Run (() => {
                var sw = new Stopwatch ();
                sw.Start ();
                Logger?.log(Level.Info, "-->sw--> start 00m:00s 000");
                //Try to get real roomId
                var realRoomIdTextTemp = biliApi.getRealRoomId (OriginRoomIdText);
                Logger?.log(Level.Info, $"-->sw--> fetched real roomId at {sw.Elapsed.ToString("mm'm:'ss's 'fff")}");
                int realRoomIdTemp;
                //Try to get flv url
                if (!string.IsNullOrEmpty (realRoomIdTextTemp) && int.TryParse (realRoomIdTextTemp, out realRoomIdTemp)) {
                    var flvUrl = biliApi.getRealUrl (realRoomIdTextTemp);
                    Logger?.log(Level.Info, $"-->sw--> fetched real url at {sw.Elapsed.ToString("mm'm:'ss's 'fff")}");
                    if (!string.IsNullOrEmpty (flvUrl)) {
                        this.RealRoomId = realRoomIdTemp;
                        this.RealRoomIdText = realRoomIdTextTemp;
                        this.FlvAddress = flvUrl;
                        return true;
                    }
                }
                return false;
            });
        }

        public Task fetchRoomInfoAsync() {
            return Task.Run(() => {
                if(RealRoomId == 0) {
                    var realRoomIdTextTemp = biliApi.getRealRoomId(OriginRoomIdText);
                    int realRoomIdTemp;
                    if (int.TryParse(realRoomIdTextTemp, out realRoomIdTemp)) {
                        this.RealRoomId = realRoomIdTemp;
                    }
                    else return;
                }
                this.RoomInfo = biliApi.getRoomInfo(RealRoomId);
                Logger?.log(Level.Info, $"Fetched room info.");
            });
        }
    }
}
