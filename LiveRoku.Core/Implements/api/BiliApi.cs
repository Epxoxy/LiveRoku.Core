namespace LiveRoku.Core.Api {
    using System;
    using System.Xml.Linq;
    using LiveRoku.Base;
    using LiveRoku.Base.Logger;
    using Newtonsoft.Json.Linq;
    using LiveRoku.Core.Models;
    using System.Diagnostics;
    using System.Text;
    using System.Globalization;

    public class BiliApi : IWebApi {

        public static class Const {
            public const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/62.0.3202.75 Safari/537.36";
            public const string AppKey = "<Bilibili App Key Here>";
            public const string SecretKey = "<Bilibili App Secret Key Here>";
            public const string CidUrl = "http://live.bilibili.com/api/player?id=cid:";
            public static readonly string[] DefaultHosts = new string[2] { "livecmt-2.bilibili.com", "livecmt-1.bilibili.com" };
            public const string DefaultChatHost = "chat.bilibili.com";
            public const int DefaultChatPort = 2243;
        }

        private readonly Encoding encoding = Encoding.UTF8;
        private readonly string userAgent;
        private readonly string appKey;
        private readonly string secretKey;
        private readonly IWebClient client;
        private readonly ILogger logger;

        public BiliApi(IWebClient client, ILogger logger, string userAgent,
            string appKey = Const.AppKey,
            string secretKey = Const.SecretKey) {
            this.client = client;
            this.logger = logger;
            this.appKey = appKey;
            this.secretKey = secretKey;
            this.userAgent = string.IsNullOrEmpty(userAgent) ? Const.UserAgent : userAgent;
            this.client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml");
            this.client.DefaultRequestHeaders.Add("User-Agent", this.userAgent);
            this.client.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.8,en;q=0.6,ja;q=0.4");
        }

        public bool tryGetValidDmServerBean(string roomId, out ServerData data) {
            data = getDmServerData(roomId);
            return data != null;
        }

        public bool tryGetRoomIdAndUrl(string roomId, out string realRoomId, out string videoUrl) {
            videoUrl = string.Empty;
            realRoomId = getRealRoomId(roomId);
            return !string.IsNullOrEmpty(realRoomId) && tryGetVideoUrl(realRoomId, out videoUrl);
        }

        public bool tryGetVideoUrl(string realRoomId, out string realUrl) {
            realUrl = string.Empty;
            try {
                realUrl = getVideoUrl(realRoomId);
            } catch (Exception e) {
                e.printStackTrace("biliApi");
                logger.log(Level.Error, "Cannot video url, Error : " + e.Message);
                return false;
            }
            return !string.IsNullOrEmpty(realUrl);
        }

        public ServerData getDmServerData(string roomId, bool useDefaultOnException = false) {
            ServerData data = null;
            //Get real danmaku server url
            //Download xml file
            bool pageNotFound = false;
            bool canUseDefault = true;
            string xmlText = null;
            try {
                xmlText = client.GetStringAsyncUsing(Const.CidUrl + roomId).Result;
            } catch (System.Net.WebException e) {
                e.printStackTrace("biliApi");
                var errorResponse = e.Response as System.Net.HttpWebResponse;
                if (e.Status == System.Net.WebExceptionStatus.ConnectFailure)
                    canUseDefault = false;
                if (errorResponse?.StatusCode == System.Net.HttpStatusCode.NotFound) {
                    logger.log(Level.Error, $"Maybe {roomId} is not a valid room id.");
                    pageNotFound = true;
                } else {
                    logger.log(Level.Error, "Download cid xml fail : " + e.Message);
                }
            } catch (Exception e) {
                e.printStackTrace("biliApi");
                logger.log(Level.Error, "Download cid xml fail : " + e.Message);
            }
            //Analyzing danmaku Xml
            if (!string.IsNullOrEmpty(xmlText)) {
                XElement doc = null;
                try {
                    doc = XElement.Parse("<root>" + xmlText + "</root>");
                    string hostText = doc.Element("dm_server").Value;
                    string portText = doc.Element("dm_port").Value;
                    if (int.TryParse(portText, out int port)) {
                        data = new ServerData {
                            Host = hostText,
                            Port = port
                        };
                    }
                } catch (Exception e) {
                    e.printStackTrace("biliApi");
                    logger.log(Level.Error, "Analyzing XML fail : " + e.Message);
                }
            }
            if (data == null && useDefaultOnException && canUseDefault && !pageNotFound) {
                var hosts = BiliApi.Const.DefaultHosts;
                data = new ServerData {
                    Host = hosts[new Random().Next(hosts.Length)],
                    Port = BiliApi.Const.DefaultChatPort
                };
            }
            return data;
        }

        public string getRealRoomId(string shortRoomId) {
            Debug.WriteLine("Trying to get real roomId", "biliApi");

            var roomWebPageUrl = "https://api.live.bilibili.com/room/v1/Room/room_init?id=" + shortRoomId;

            string roomJsonText;
            try {
                roomJsonText = client.GetStringAsyncUsing(roomWebPageUrl).Result;
                if (string.IsNullOrEmpty(roomJsonText))
                    throw new Exception($"Cannot download anything from {roomWebPageUrl}");
            } catch (Exception e) {
                logger.log(Level.Error, "Open live page fail : " + e.Message);
                return null;
            }

            JToken message = null;
            try {
                var result = JObject.Parse(roomJsonText);
                result.TryGetValue("message", out message);
                var roomId = result["data"]["room_id"].ToString();
                return roomId;
            } catch (Exception e) {
                logger.log(Level.Error, "Fail Get Real Room Id: " + message?.ToString());
                Debug.WriteLine("Parse roomId fail, " + e.ToString(), "biliApi");
            }

            return null;
        }

        public string getVideoUrl(string realRoomId) {
            if (string.IsNullOrEmpty(realRoomId)) {
                logger.log(Level.Error, "Invalid operation, No roomId");
                return string.Empty;
                throw new Exception("No roomId");
            }
            ///var apiUrl = createApiUrl (roomId);
            var apiUrl = "https://api.live.bilibili.com/api/playurl?cid=" + realRoomId + "&otype=json&quality=0&platform=web";

            string jsonText;
            //Get xml by API
            try {
                jsonText = client.GetStringAsyncUsing(apiUrl).Result;
            } catch (Exception e) {
                logger.log(Level.Error, "Fail sending analysis request : " + e.Message);
                throw;
            }

            //Analyzing xml
            string realUrl = string.Empty;
            try {
                var jsonObj = JObject.Parse(jsonText);
                realUrl = jsonObj["durl"][0]["url"].ToString();
                /*var playUrlXml = XDocument.Parse (xmlText);
                // var result = playUrlXml.XPathSelectElement ("/video/result");
                //Get analyzing result
                if (result == null || !"suee".Equals (result.Value)) {
                    logger.log (Level.Error, "Analyzing url address fail");
                    throw new Exception ("No Avaliable download url in xml information.");
                }
                realUrl = playUrlXml.XPathSelectElement ("/video/durl/url").Value;*/
                Debug.WriteLine("Get video url : " + realUrl, "biliApi");
            } catch (Exception e) {
                e.printStackTrace("biliApi");
                logger.log(Level.Error, "Analyzing JSON fail : " + e.Message);
                throw;
            }
            return realUrl;
        }

        private LiveStatus getStatusByCode(int statusCode) {
            switch (statusCode) {
                case 0:
                    return LiveStatus.Preparing;
                case 1:
                    return LiveStatus.Live;
                case 2:
                    return LiveStatus.Round;
            }
            return LiveStatus.Preparing;
        }

        //TODO complete
        public IRoomInfo getRoomInfo(string realRoomId) {
            string url = $"http://api.live.bilibili.com/room/v1/Room/get_info?room_id={realRoomId}&from=room";
            string infoJson = null;
            try {
                infoJson = client.GetStringAsyncUsing(url).Result;
            } catch (Exception e) {
                logger.log(Level.Error, "Open live/getInfo page fail : " + e.Message);
            }
            if (string.IsNullOrEmpty(infoJson))
                return null;
            try {
                var data = JObject.Parse(infoJson)["data"];
                //Debug.WriteLine("RoomInfo: " + infoJson, "biliApi");
                //logger.log(Level.Info, infoJson);
                if (data != null && data.Type != JTokenType.Null && data.Type != JTokenType.Undefined &&
                    data.HasValues) {
                    //LiveCode 0->live off, 1->live on, 2->round
                    var statusCode = data.Value<int>("live_status");
                    var title = data.Value<string>("title");
                    var liveTime = data.Value<string>("live_time");
                    //string statusText = data.Value<string>("_status");
                    //string liveStatusText = data.Value<string>("LIVE_STATUS");
                    //string title = data.Value<string>("ROOMTITLE");
                    var detail = new RoomInfo(infoJson) {
                        IsOn = statusCode == 1,
                        LiveStatus = getStatusByCode(statusCode),
                        Title = title,
                        //TimeLine = data.Value<int>("LIVE_TIMELINE"),
                        //Anchor = data.Value<string>("ANCHOR_NICK_NAME")
                    };
                    if (!liveTime.StartsWith("0000")) {
                        try {//get live start time
                            detail.TimeLine = (int)Convert.ToDateTime(liveTime, new DateTimeFormatInfo {
                                FullDateTimePattern = "yyyy-MM-dd hh:mm:ss"
                            }).totalMsToGreenTime();
                        } catch (Exception e) {
                            Debug.WriteLine($"Error coverting datetime {liveTime} : {e.Message} ", "biliApi");
                        }
                    }
                    Debug.WriteLine($"LiveStatus {detail.LiveStatus}, StatusCode {statusCode} ", "biliApi");
                    Debug.WriteLine($"RoomTitle {title}", "biliApi");
                    return detail;
                }
            } catch (Exception e) {
                logger.log(Level.Error, "Parse room info fail : " + e.Message);
                e.printStackTrace("biliApi");
            }
            return null;
        }

        /*@deprecated
        private string createApiUrl (string roomId) {
            //Generate parameters
            var apiParams = new StringBuilder ().Append ("appkey=").Append (appKey).Append ("&")
                .Append ("cid=").Append (roomId).Append ("&")
                .Append ("player=1&quality=0&ts=");
            var ts = DateTime.UtcNow - new DateTime (1970, 1, 1, 0, 0, 0, 0); //UNIX TimeStamp
            apiParams.Append (Convert.ToInt64 (ts.TotalSeconds).ToString ());

            var apiParam = apiParams.ToString (); //Origin parameters string

            //Generate signature
            var waitForSign = apiParam + secretKey;
            var waitForSignBytes = Encoding.UTF8.GetBytes (waitForSign);
            MD5 md5 = new MD5CryptoServiceProvider ();
            var signBytes = md5.ComputeHash (waitForSignBytes);

            var sign = signBytes.Aggregate ("", (current, t) => current + t.ToString ("x"));

            //Final API
            return "http://live.bilibili.com/api/playurl?" + apiParam + "&sign=" + sign;
        }*/
    }
}