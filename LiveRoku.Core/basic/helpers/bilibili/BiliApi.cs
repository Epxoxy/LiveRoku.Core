using System;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.XPath;
using LiveRoku.Base;
using Newtonsoft.Json.Linq;

namespace LiveRoku.Core {

    public class BiliApi {

        public static class Const {
            public const string AppKey = "<Bilibili App Key Here>";
            public const string SecretKey = "<Bilibili App Secret Key Here>";
            public const string CidUrl = "http://live.bilibili.com/api/player?id=cid:";
            public static readonly string[] DefaultHosts = new string[2] { "livecmt-2.bilibili.com", "livecmt-1.bilibili.com" };
            public const string DefaultChatHost = "chat.bilibili.com";
            public const int DefaultChatPort = 2243;
        }

        public readonly string userAgent;
        private readonly ILogger logger;
        private readonly StatHelper statHelper;

        public BiliApi (ILogger logger, string userAgent, StatHelper statHelper = null) {
            this.logger = logger;
            this.userAgent = userAgent;
            this.statHelper = statHelper;
        }

        public bool getDmServerAddr (string roomId, out string url, out string port, out bool mayNotExist) {
            //Get real danmaku server url
            url = string.Empty;
            port = string.Empty;
            mayNotExist = false;
            //Download xml file
            var client = getBaseWebClient ();
            string xmlText = null;
            try {
                xmlText = client.DownloadString (Const.CidUrl + roomId);
            } catch (WebException e) {
                e.printStackTrace ();
                var errorResponse = e.Response as HttpWebResponse;
                if (errorResponse != null && errorResponse.StatusCode == HttpStatusCode.NotFound) {
                    logger.appendLine ("ERROR", $"Maybe {roomId} is not a valid room id.");
                    mayNotExist = true;
                } else {
                    logger.appendLine ("ERROR", "Download cid xml fail : " + e.Message);
                }
            } catch (Exception e) {
                e.printStackTrace ();
                logger.appendLine ("ERROR", "Download cid xml fail : " + e.Message);
            }
            if (string.IsNullOrEmpty (xmlText)) {
                return false;
            }

            //Analyzing danmaku Xml
            XElement doc = null;
            try {
                doc = XElement.Parse ("<root>" + xmlText + "</root>");
                url = doc.Element ("dm_server").Value;
                port = doc.Element ("dm_port").Value;
                return true;
            } catch (Exception e) {
                e.printStackTrace ();
                logger.appendLine ("ERROR", "Analyzing XML fail : " + e.Message);
                return false;
            }
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
            logger.appendLine ("INFO", "Trying to get real roomId");

            var roomWebPageUrl = "http://live.bilibili.com/" + originalRoomId;
            var wc = new WebClient ();
            wc.Headers.Add ("Accept: text/html");
            wc.Headers.Add ("User-Agent: " + userAgent);
            wc.Headers.Add ("Accept-Language: zh-CN,zh;q=0.8,en;q=0.6,ja;q=0.4");
            string roomHtml;

            try {
                roomHtml = wc.DownloadString (roomWebPageUrl);
            } catch (Exception e) {
                logger.appendLine ("ERROR", "Open live page fail : " + e.Message);
                return null;
            }

            //Get real room id from HTML
            const string pattern = @"(?<=var ROOMID = )(\d+)(?=;)";
            var cols = Regex.Matches (roomHtml, pattern);
            foreach (Match mat in cols) {
                logger.appendLine ("INFO", "Real Room Id : " + mat.Value);
                return mat.Value;
            }

            logger.appendLine ("ERROR", "Fail Get Real Room Id");
            return null;
        }

        public bool tryGetRealUrl (string realRoomId, out string realUrl) {
            realUrl = string.Empty;
            try {
                realUrl = getRealUrl (realRoomId);
            } catch (Exception e) {
                e.printStackTrace ();
                logger.appendLine ("ERROR", "Get real url fail, Msg : " + e.Message);
                return false;
            }
            return !string.IsNullOrEmpty (realUrl);
        }

        public string getRealUrl (string roomId) {
            if (roomId == null) {
                logger.appendLine ("ERROR", "Invalid operation, No roomId");
                return string.Empty;
                throw new Exception ("No roomId");
            }
            var apiUrl = createApiUrl (roomId);

            string xmlText;

            //Get xml by API
            var wc = getBaseWebClient ();
            try {
                xmlText = wc.DownloadString (apiUrl);
            } catch (Exception e) {
                logger.appendLine ("ERROR", "Fail sending analysis request : " + e.Message);
                throw e;
            }

            //Analyzing xml
            string realUrl = string.Empty;
            try {
                var playUrlXml = XDocument.Parse (xmlText);
                var result = playUrlXml.XPathSelectElement ("/video/result");
                //Get analyzing result
                if (result == null || !"suee".Equals (result.Value)) { //Same to use != for string type
                    logger.appendLine ("ERROR", "Analyzing url address fail");
                    throw new Exception ("No Avaliable download url in xml information.");
                }
                realUrl = playUrlXml.XPathSelectElement ("/video/durl/url").Value;
            } catch (Exception e) {
                e.printStackTrace ();
                logger.appendLine ("ERROR", "Analyzing XML fail : " + e.Message);
                throw e;
            }
            if (!string.IsNullOrEmpty (realUrl)) {
                logger.appendLine ("INFO", "Analyzing url address successful : " + realUrl);
            }
            return realUrl;
        }

        //TODO complete
        public RoomInfo getRoomInfo (int realRoomId) {
            string url = $"https://live.bilibili.com/live/getInfo?roomid={realRoomId}";
            var wc = new WebClient ();
            wc.Headers.Add ("Accept: text/html");
            wc.Headers.Add ("User-Agent: " + userAgent);
            wc.Headers.Add ("Accept-Language: zh-CN,zh;q=0.8,en;q=0.6,ja;q=0.4");
            string infoJson;

            try {
                infoJson = wc.DownloadString (url);
                var data = JObject.Parse (infoJson)["data"];
                logger.appendLine ("Info", infoJson);
                if (data != null && data.Type != JTokenType.Null && data.Type != JTokenType.Undefined &&
                    data.HasValues) {
                    logger.appendLine ("_status", data.Value<string> ("_status"));
                    logger.appendLine ("LiveStatus", data.Value<string> ("LIVE_STATUS"));
                    logger.appendLine ("RoomTitle", data.Value<string> ("ROOMTITLE"));

                    LiveStatus status;
                    Enum.TryParse (data.Value<string> ("LIVE_STATUS"), true, out status);
                    var info = new RoomInfo ();
                    info.IsOn = "on".Equals (data.Value<string> ("_status").ToLower ());
                    info.LiveStatus = status;
                    info.Title = data.Value<string> ("ROOMTITLE");
                    info.TimeLine = data.Value<int>("LIVE_TIMELINE");
                    info.Anchor = data.Value<string>("ANCHOR_NICK_NAME");
                    return info;
                }
            } catch (Exception e) {
                logger.appendLine ("ERROR", "Open live page fail : " + e.Message);
                e.printStackTrace();
            }
            return null;
        }

        private string createApiUrl (string roomId) {
            //Generate parameters
            var apiParams = new StringBuilder ().Append ("appkey=").Append (Const.AppKey).Append ("&")
                .Append ("cid=").Append (roomId).Append ("&")
                .Append ("player=1&quality=0&ts=");
            var ts = DateTime.UtcNow - new DateTime (1970, 1, 1, 0, 0, 0, 0); //UNIX TimeStamp
            apiParams.Append (Convert.ToInt64 (ts.TotalSeconds).ToString ());

            var apiParam = apiParams.ToString (); //Origin parameters string

            //Generate signature
            var waitForSign = apiParam + Const.SecretKey;
            var waitForSignBytes = Encoding.UTF8.GetBytes (waitForSign);
            MD5 md5 = new MD5CryptoServiceProvider ();
            var signBytes = md5.ComputeHash (waitForSignBytes);

            var sign = signBytes.Aggregate ("", (current, t) => current + t.ToString ("x"));

            //Final API
            return "http://live.bilibili.com/api/playurl?" + apiParam + "&sign=" + sign;
        }

        private WebClient getBaseWebClient () {
            var client = new WebClient ();
            client.Headers.Add ("Accept: */*");
            client.Headers.Add ("User-Agent: " + userAgent);
            client.Headers.Add ("Accept-Language: zh-CN,zh;q=0.8,en;q=0.6,ja;q=0.4");
            return client;
        }
    }
}