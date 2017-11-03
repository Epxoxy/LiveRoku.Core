namespace LiveRoku.Core {
    using System.Diagnostics;
    using LiveRoku.Base;
    using LiveRoku.Base.Logger;
    internal class FetchArgsBean {
        public string ShortRoomId { get; private set; }
        public string RealRoomId { get; private set; }
        public string FlvAddress { get; private set; }
        public bool IsShortIdTheRealId { get; set; }
        public IRoomInfo RoomInfo { get; private set; }
        public string Folder { get; set; }
        public string FileNameFormat { get; set; }
        public bool AutoStart { get; set; }
        public bool DanmakuRequire { get; set; }
        public bool VideoRequire { get; set; }
        public ILogger Logger { get; set; }
        //private
        private readonly BiliApi accessApi;
        private object fetchLocker = new object ();

        public FetchArgsBean (string originRoomId, BiliApi accessApi, ILogger logger) {
            this.ShortRoomId = originRoomId;
            this.accessApi = accessApi;
            this.Logger = logger;
        }

        public FetchArgsBean (BiliApi accessApi, ILogger logger) : this (null, accessApi, logger) { }
        public FetchArgsBean (string originRoomId, BiliApi accessApi) : this (originRoomId, accessApi, null) { }

        public void resetOriginId (string originRoomId) {
            this.ShortRoomId = originRoomId;
            this.RealRoomId = null;
            this.FlvAddress = null;
        }
        
        private bool fetchRealRoomId(out string resultId) {
            //Try to get real roomId
            resultId = null;
            if (IsShortIdTheRealId) {
                resultId = ShortRoomId;
                return true;
            } else {
                resultId = accessApi.getRealRoomId(ShortRoomId);
                Logger?.log(Level.Info, $"-->sw--> fetched real roomId {resultId}");
                return (!string.IsNullOrEmpty(resultId) && int.TryParse(resultId, out int idTemp));
            }
        }

        public bool fetchUrlAndRealId () {
            var sw = new Stopwatch ();
            sw.Start ();
            Logger?.log (Level.Info, "-->sw--> start 00m:00s 000");
            //Try to get flv url
            if (fetchRealRoomId(out string idTextTemp)) {
                var flvUrl = accessApi.getRealUrl(idTextTemp);
                Logger?.log(Level.Info, $"-->sw--> fetched real url at {sw.Elapsed.ToString("mm'm:'ss's 'fff")}");
                sw.Stop();
                if (!string.IsNullOrEmpty(flvUrl)) {
                    this.RealRoomId = idTextTemp;
                    this.FlvAddress = flvUrl;
                    return true;
                }
            }
            sw.Stop ();
            return false;
        }

        //Will auto get RealRoomId if not valid
        public IRoomInfo fetchRoomInfo () {
            if (string.IsNullOrEmpty(RealRoomId)) {
                if (fetchRealRoomId(out string idTextTemp)) {
                    this.RealRoomId = idTextTemp;
                } else return this.RoomInfo;
            }
            this.RoomInfo = accessApi.getRoomInfo(RealRoomId);
            Logger?.log(Level.Info, $"Fetched room info.");
            return this.RoomInfo;
        }
    }
}