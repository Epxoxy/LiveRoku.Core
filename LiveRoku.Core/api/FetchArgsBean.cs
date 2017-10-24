using System.Diagnostics;
using LiveRoku.Base;
using LiveRoku.Base.Logger;
namespace LiveRoku.Core {
    internal class FetchArgsBean {
        public int OriginRoomId { get; private set; }
        public int RealRoomId { get; private set; }
        public string FlvAddress { get; private set; }
        public IRoomInfo RoomInfo { get; private set; }
        public string Folder { get; set; }
        public string FileNameFormat { get; set; }
        public bool AutoStart { get; set; }
        public bool DanmakuRequire { get; set; }
        public bool VideoRequire { get; set; }
        public ILogger Logger { get; set; }
        private readonly BiliApi biliApi;
        private object fetchLocker = new object ();

        public FetchArgsBean (int originRoomId, BiliApi biliApi, ILogger logger) {
            this.OriginRoomId = originRoomId;
            this.biliApi = biliApi;
            this.Logger = logger;
        }

        public FetchArgsBean (BiliApi biliApi, ILogger logger) : this (-1, biliApi, logger) { }
        public FetchArgsBean (int originRoomId, BiliApi biliApi) : this (originRoomId, biliApi, null) { }

        public void resetOriginId (int originRoomId) {
            this.OriginRoomId = originRoomId;
            this.RealRoomId = default (int);
            this.FlvAddress = null;
        }

        public bool fetchUrlAndRealId () {
            var sw = new Stopwatch ();
            sw.Start ();
            Logger?.log (Level.Info, "-->sw--> start 00m:00s 000");
            //Try to get real roomId
            var idTextTemp = biliApi.getRealRoomId (OriginRoomId.ToString ());
            Logger?.log (Level.Info, $"-->sw--> fetched real roomId at {sw.Elapsed.ToString("mm'm:'ss's 'fff")}");
            //Try to get flv url
            if (!string.IsNullOrEmpty (idTextTemp) && int.TryParse (idTextTemp, out int idTemp)) {
                var flvUrl = biliApi.getRealUrl (idTextTemp);
                Logger?.log (Level.Info, $"-->sw--> fetched real url at {sw.Elapsed.ToString("mm'm:'ss's 'fff")}");
                sw.Stop ();
                if (!string.IsNullOrEmpty (flvUrl)) {
                    this.RealRoomId = idTemp;
                    this.FlvAddress = flvUrl;
                    return true;
                }
            }
            sw.Stop ();
            return false;
        }

        //Will auto get RealRoomId if not valid
        public void fetchRoomInfo () {
            if (RealRoomId <= 0) {
                var realRoomIdTextTemp = biliApi.getRealRoomId (OriginRoomId.ToString ());
                if (int.TryParse (realRoomIdTextTemp, out int realRoomIdTemp)) {
                    this.RealRoomId = realRoomIdTemp;
                } else return;
            }
            this.RoomInfo = biliApi.getRoomInfo (RealRoomId);
            Logger?.log (Level.Info, $"Fetched room info.");
        }
    }
}