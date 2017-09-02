namespace LiveRoku.Core {
    using System.Net;
    using System;
    public class StatHelper {
        public readonly string osVer;
        public readonly string appVer;
        public StatHelper (string osVer, string appVer) {
            this.osVer = osVer;
            this.appVer = appVer;
        }
        public void sendStat (string roomId, string userAgent) {
            var api = $"https://zyzsdy.com/biliroku/stat?os={osVer}&id={roomId}&ver={appVer}";

            var wc = new WebClient ();
            wc.Headers.Add ("Accept: */*");
            wc.Headers.Add ("User-Agent: " + userAgent);
            wc.Headers.Add ("Accept-Language: zh-CN,zh;q=0.8,en;q=0.6,ja;q=0.4");
            try {
                wc.DownloadStringAsync (new Uri (api));
            } catch {
                //ignore
            }
        }
    }
}