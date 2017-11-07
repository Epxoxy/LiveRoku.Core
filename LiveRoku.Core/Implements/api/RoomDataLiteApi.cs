namespace LiveRoku.Core {
    using System.Diagnostics;
    using LiveRoku.Base;
    using LiveRoku.Base.Logger;
    using LiveRoku.Core.Models;
    internal class RoomDataLiteApi {
        public string ShortRoomId { get; private set; }
        public string RealRoomId { get; private set; }
        public string VideoUrl { get; private set; }
        public bool IsShortIdTheRealId { get; set; }
        public IRoomInfo RoomInfo { get; private set; }
        public ILogger Logger { get; set; }
        //private
        private readonly IWebApi accessApi;
        private object fetchLocker = new object ();

        public RoomDataLiteApi (string shortId, IWebApi accessApi, ILogger logger) {
            this.ShortRoomId = shortId;
            this.accessApi = accessApi;
            this.Logger = logger;
        }

        public RoomDataLiteApi (IWebApi accessApi, ILogger logger) : this (null, accessApi, logger) { }
        public RoomDataLiteApi (string originRoomId, IWebApi accessApi) : this (originRoomId, accessApi, null) { }

        public void resetShortId (string shortId) {
            this.ShortRoomId = shortId;
            this.RealRoomId = null;
            this.VideoUrl = null;
        }
        
        public bool fetchRealId() {
            if(fetchRealIdImpl(out string idTextTemp)) {
                this.RealRoomId = idTextTemp;
                return true;
            }
            return false;
        }

        public bool fetchRealIdAndUrl (string invoker) {
            var sw = new Stopwatch ();
            sw.Start ();
            Debug.WriteLine($"{invoker}-->sw--> start 00m:00s 000", "watch");
            //Try to get flv url
            if (fetchRealIdImpl(out string idTextTemp)) {
                var flvUrl = accessApi.getVideoUrl(idTextTemp);
                Debug.WriteLine($"{invoker}-->sw--> fetched real url at {sw.Elapsed.ToString("mm'm:'ss's 'fff")}", "watch");
                sw.Stop();
                if (!string.IsNullOrEmpty(flvUrl)) {
                    this.RealRoomId = idTextTemp;
                    this.VideoUrl = flvUrl;
                    return true;
                }
            }
            sw.Stop ();
            return false;
        }

        //Will auto get RealRoomId if not valid
        public IRoomInfo fetchRoomInfo () {
            if (string.IsNullOrEmpty(RealRoomId)) {
                if (fetchRealIdImpl(out string idTextTemp)) {
                    this.RealRoomId = idTextTemp;
                } else return this.RoomInfo;
            }
            this.RoomInfo = accessApi.getRoomInfo(RealRoomId);
            Logger?.log(Level.Info, $"Fetched RoomInfo, Title {this.RoomInfo?.Title}.");
            return this.RoomInfo;
        }

        private bool fetchRealIdImpl(out string resultId) {
            //Try to get real roomId
            resultId = null;
            if (IsShortIdTheRealId) {
                resultId = ShortRoomId;
                return true;
            } else {
                resultId = accessApi.getRealRoomId(ShortRoomId);
                Debug.WriteLine($"-->sw--> fetched real roomId {resultId}", "watch");
                return (!string.IsNullOrEmpty(resultId) && int.TryParse(resultId, out int idTemp));
            }
        }

    }
}