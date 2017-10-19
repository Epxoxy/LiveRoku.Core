using System;
using System.Collections.Generic;
using System.Linq;
using LiveRoku.Base;
using Newtonsoft.Json.Linq;

namespace LiveRoku.Core {
    public class DanmakuFactory {
        public static DanmakuModel parse (string jsonText, long createTime, int version) {
            var d = new DanmakuModel (jsonText, createTime, version);
            switch (version) {
                case 1:
                    var obj = JArray.Parse (jsonText);
                    d.MsgType = MsgTypeEnum.Comment;
                    d.CommentText = obj[1].ToString ();
                    d.UserName = obj[2][1].ToString ();
                    break;
                case 2:
                    try {
                        resolveVersion2 (ref d, JObject.Parse (jsonText));
                    } catch (Exception e) {
                        System.Diagnostics.Debug.WriteLine (jsonText);
                        System.Diagnostics.Debug.WriteLine (e.Message);
                        System.Diagnostics.Debug.WriteLine (e.TargetSite);
                        //e.printStackTrace ();
                    }
                    break;
                default:
                    throw new Exception ();
            }
            return d;
        }

        private static void resolveVersion2 (ref DanmakuModel d, JObject obj) {
            string cmd = obj["cmd"].ToString ();
            switch (cmd) {
                case "LIVE":
                    d.MsgType = MsgTypeEnum.LiveStart;
                    d.roomID = obj["roomid"].ToString ();
                    break;
                case "PREPARING":
                    d.MsgType = MsgTypeEnum.LiveEnd;
                    d.roomID = obj["roomid"].ToString ();
                    break;
                case "DANMU_MSG":
                    d.MsgType = MsgTypeEnum.Comment;
                    resolveDanmakuMsg (ref d, obj);
                    break;
                case "SEND_GIFT":
                    d.MsgType = MsgTypeEnum.GiftSend;
                    d.GiftName = obj["data"]["giftName"].ToString ();
                    d.UserName = obj["data"]["uname"].ToString ();
                    d.UserID = obj["data"]["uid"].ToObject<int> ();
                    // Giftrcost = obj["data"]["rcost"].ToString();
                    d.GiftCount = obj["data"]["num"].ToObject<int> ();
                    break;
                case "GIFT_TOP":
                    d.MsgType = MsgTypeEnum.GiftTop;
                    resolveGifTop (ref d, obj);
                    break;
                case "WELCOME":
                    d.MsgType = MsgTypeEnum.Welcome;
                    d.UserName = obj["data"]["uname"].ToString ();
                    d.UserID = obj["data"]["uid"].ToObject<int> ();
                    d.isVIP = true;
                    d.isAdmin = obj["data"]["isadmin"].ToString () == "1";
                    break;
                case "WELCOME_GUARD":
                    d.MsgType = MsgTypeEnum.WelcomeGuard;
                    d.UserName = obj["data"]["username"].ToString ();
                    d.UserID = obj["data"]["uid"].ToObject<int> ();
                    d.UserGuardLevel = obj["data"]["guard_level"].ToObject<int> ();
                    break;
                case "GUARD_BUY":
                    d.MsgType = MsgTypeEnum.GuardBuy;
                    d.UserID = obj["data"]["uid"].ToObject<int> ();
                    d.UserName = obj["data"]["username"].ToString ();
                    d.UserGuardLevel = obj["data"]["guard_level"].ToObject<int> ();
                    d.GiftName = d.UserGuardLevel == 3 ? "舰长" : d.UserGuardLevel == 2 ? "提督" : d.UserGuardLevel == 1 ? "总督" : "";
                    d.GiftCount = obj["data"]["num"].ToObject<int> ();
                    break;
                default:
                    d.MsgType = MsgTypeEnum.Unknown;
                    break;
            }
        }

        private static void resolveGifTop (ref DanmakuModel d, JObject obj) {
            var alltop = obj["data"].ToList ();
            d.GiftRanking = new List<GiftRank> ();
            foreach (var v in alltop) {
                d.GiftRanking.Add (new GiftRank () {
                    Uid = v.Value<int> ("uid"),
                        UserName = v.Value<string> ("uname"),
                        Coin = v.Value<decimal> ("coin")

                });
            }
        }

        private static void resolveDanmakuMsg (ref DanmakuModel d, JObject obj) {
            var data = (JArray) obj["info"];
            var length = data.Count;
            if (length > 7) {
                d.UserGuardLevel = data[7].ToObject<int> ();
            }
            d.CommentText = data[1].ToString ();
            d.UserID = data[2][0].ToObject<int> ();
            d.UserName = data[2][1].ToString ();
            d.isAdmin = data[2][2].ToString () == "1";
            d.isVIP = data[2][3].ToString () == "1";
            //Get text only danmaku extension
            d.DmType = Convert.ToInt32 (data[0][1]);
            d.Fontsize = Convert.ToInt32 (data[0][2]);
            d.Color = Convert.ToInt32 (data[0][3]);
            d.SendTimestamp = Convert.ToInt64 (data[0][4]);
            d.UserHash = data[0][7].ToString ();
        }
    }
}