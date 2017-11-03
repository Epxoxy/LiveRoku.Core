namespace LiveRoku.Core {
    using System;
    using System.Xml.Linq;
    using LiveRoku.Base;
    using LiveRoku.Base.Logger;
    using Newtonsoft.Json.Linq;
    using LiveRoku.Core.Models;
    public class BiliApi {

        public static class Const {
            public const string AppKey = "<Bilibili App Key Here>";
            public const string SecretKey = "<Bilibili App Secret Key Here>";
            public const string CidUrl = "http://live.bilibili.com/api/player?id=cid:";
            public static readonly string[] DefaultHosts = new string[2] { "livecmt-2.bilibili.com", "livecmt-1.bilibili.com" };
            public const string DefaultChatHost = "chat.bilibili.com";
            public const int DefaultChatPort = 2243;
        }

        private readonly string userAgent;
        private readonly ILogger logger;
        private readonly string appKey;
        private readonly string secretKey;
        private readonly Func<IWebClient> createWebClient;

        public BiliApi (Func<IWebClient> howCreateWebClient, ILogger logger, string userAgent,
            string appKey = Const.AppKey, string secretKey = Const.SecretKey) {
            this.createWebClient = howCreateWebClient;
            this.logger = logger;
            this.userAgent = userAgent;
            this.appKey = appKey;
            this.secretKey = secretKey;
        }

        public FetchServerResult getDmServerAddr (string roomId) {
            var bean = new FetchServerResult ();
            //Get real danmaku server url
            //Download xml file
            var client = newBaseWebClient ();
            string xmlText = null;
            try {
                xmlText = client.DownloadString (Const.CidUrl + roomId);
            } catch (System.Net.WebException e) {
                e.printStackTrace();
                var errorResponse = e.Response as System.Net.HttpWebResponse;
                if (e.Status == System.Net.WebExceptionStatus.ConnectFailure)
                    bean.CanUseDefault = false;
                if (errorResponse?.StatusCode == System.Net.HttpStatusCode.NotFound) {
                    logger.log (Level.Error, $"Maybe {roomId} is not a valid room id.");
                    bean.MayNotExist = true;
                } else {
                    logger.log (Level.Error, "Download cid xml fail : " + e.Message);
                }
            } catch (Exception e) {
                e.printStackTrace();
                logger.log (Level.Error, "Download cid xml fail : " + e.Message);
            }
            if (string.IsNullOrEmpty (xmlText)) {
                return bean;
            }

            //Analyzing danmaku Xml
            XElement doc = null;
            try {
                doc = XElement.Parse ("<root>" + xmlText + "</root>");
                string hostText = doc.Element ("dm_server").Value;
                string portText = doc.Element ("dm_port").Value;
                if (int.TryParse (portText, out int port)) {
                    bean.Host = hostText;
                    bean.Port = port;
                    bean.FetchOK = true;
                }
            } catch (Exception e) {
                e.printStackTrace();
                logger.log (Level.Error, "Analyzing XML fail : " + e.Message);
            }
            return bean;
        }

        public bool tryGetValidDmServerBean (string roomId, out FetchServerResult bean) {
            bean = getDmServerAddr (roomId);
            if (bean != null && (bean.MayNotExist || !bean.CanUseDefault)) {
                return false;
            }
            if (bean == null || (!bean.FetchOK && bean.CanUseDefault)) {
                //May exist, generate default address
                var hosts = BiliApi.Const.DefaultHosts;
                bean.Host = hosts[new Random().Next(hosts.Length)];
                bean.Port = BiliApi.Const.DefaultChatPort;
            }
            return true;
        }

        public bool tryGetRoomIdAndUrl (string roomId, out string realRoomId, out string flvUrl) {
            flvUrl = string.Empty;
            realRoomId = getRealRoomId (roomId);
            if (string.IsNullOrEmpty (realRoomId)) {
                return false;
            }
            //Step2.Get flv url
            if (tryGetRealUrl (realRoomId, out flvUrl))
                return true;
            return false;
        }

        public string getRealRoomId (string originalRoomId) {
            logger.log (Level.Info, "Trying to get real roomId");

            var roomWebPageUrl = "https://api.live.bilibili.com/room/v1/Room/room_init?id=" + originalRoomId;
            var wc = newWebClient ();
            wc.AddHeader ("Accept: text/html");
            wc.AddHeader ("User-Agent: " + userAgent);
            wc.AddHeader ("Accept-Language: zh-CN,zh;q=0.8,en;q=0.6,ja;q=0.4");

            string roomJsonText;
            try {
                roomJsonText = wc.DownloadString (roomWebPageUrl);
                if (string.IsNullOrEmpty(roomJsonText))
                    throw new Exception($"Cannot download anything from {roomWebPageUrl}");
            } catch (Exception e) {
                logger.log (Level.Error, "Open live page fail : " + e.Message);
                return null;
            }

            JToken message = null;
            try {
                var result = JObject.Parse(roomJsonText);
                result.TryGetValue("message", out message);
                var roomId = result["data"]["room_id"].ToString();
                return roomId;
            } catch(Exception e) {
                logger.log(Level.Error, "Fail Get Real Room Id: " + message?.ToString());
                System.Diagnostics.Debug.WriteLine("Parse roomId fail, "+e.ToString(), "biliApi");
            }
            
            return null;
        }

        public bool tryGetRealUrl (string realRoomId, out string realUrl) {
            realUrl = string.Empty;
            try {
                realUrl = getRealUrl (realRoomId);
            } catch (Exception e) {
                e.printStackTrace();
                logger.log (Level.Error, "Get real url fail, Msg : " + e.Message);
                return false;
            }
            return !string.IsNullOrEmpty (realUrl);
        }

        public string getRealUrl (string roomId) {
            if (roomId == null) {
                logger.log (Level.Error, "Invalid operation, No roomId");
                return string.Empty;
                throw new Exception ("No roomId");
            }
            ///var apiUrl = createApiUrl (roomId);
            var apiUrl = "https://api.live.bilibili.com/api/playurl?cid=" + roomId + "&otype=json&quality=0&platform=web";

            string jsonText;

            //Get xml by API
            var wc = newBaseWebClient ();
            try {
                jsonText = wc.DownloadString (apiUrl);
            } catch (Exception e) {
                logger.log (Level.Error, "Fail sending analysis request : " + e.Message);
                throw e;
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
            } catch (Exception e) {
                e.printStackTrace();
                logger.log (Level.Error, "Analyzing JSON fail : " + e.Message);
                throw e;
            }
            if (!string.IsNullOrEmpty (realUrl)) {
                logger.log (Level.Info, "Analyzing url address successful : " + realUrl);
            }
            return realUrl;
        }

        //TODO complete
        public IRoomInfo getRoomInfo (string realRoomId) {
            string url = $"https://live.bilibili.com/live/getInfo?roomid={realRoomId}";
            var wc = newWebClient ();
            wc.AddHeader ("Accept: text/html");
            wc.AddHeader ("User-Agent: " + userAgent);
            wc.AddHeader ("Accept-Language: zh-CN,zh;q=0.8,en;q=0.6,ja;q=0.4");
            string infoJson = null;
            try {
                infoJson = wc.DownloadString(url);
            }catch(Exception e) {
                logger.log(Level.Error, "Open live/getInfo page fail : " + e.Message);
            }
            if (string.IsNullOrEmpty(infoJson))
                return null;
            try {
                var data = JObject.Parse (infoJson)["data"];
                System.Diagnostics.Debug.WriteLine ("RoomInfo: " + infoJson, "BiliApi");
                //logger.log(Level.Info, infoJson);
                if (data != null && data.Type != JTokenType.Null && data.Type != JTokenType.Undefined &&
                    data.HasValues) {
                    string statusText = data.Value<string> ("_status");
                    string liveStatusText = data.Value<string> ("LIVE_STATUS");
                    string title = data.Value<string> ("ROOMTITLE");
                    
                    Enum.TryParse (liveStatusText, true, out LiveStatus status);
                    var detail = new RoomInfo (infoJson){
                        IsOn = "on".Equals(statusText.ToLower()),
                        LiveStatus = status,
                        Title = title,
                        TimeLine = data.Value<int>("LIVE_TIMELINE"),
                        Anchor = data.Value<string>("ANCHOR_NICK_NAME")
                    };
                    logger.log (Level.Info, $"LiveStatus {liveStatusText}, _status {statusText} ");
                    logger.log (Level.Info, $"RoomTitle {title}");
                    return detail;
                }
            } catch (Exception e) {
                logger.log (Level.Error, "Parse room info fail : " + e.Message);
                e.printStackTrace();
            }
            return null;
        }

        private IWebClient newWebClient() => createWebClient.Invoke();

        private IWebClient newBaseWebClient() {
            var client = newWebClient();
            client.AddHeader("Accept: */*");
            client.AddHeader("User-Agent: " + userAgent);
            client.AddHeader("Accept-Language: zh-CN,zh;q=0.8,en;q=0.6,ja;q=0.4");
            return client;
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