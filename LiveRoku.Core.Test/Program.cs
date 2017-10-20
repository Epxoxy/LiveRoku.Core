using System;
using System.Diagnostics;
using LiveRoku.Base;
namespace LiveRoku.Core.Test {
    class Program : ILogHandler, IFetchSettings, ILiveProgressBinder {
        public string RoomId => roomId;
        public string Folder => "C:/mnt/hd1/station/bilibili/test/testtemp";
        public string FileFormat => "{roomId}-{Y}-{M}-{d}-{H}-{m}-{s}.flv";
        public bool DownloadDanmaku => true;
        public bool AutoStart => true;
        private ILiveFetcher fetcher;
        private string roomId;
        private string sizeText;
        private int durationUpdateTimes = 0;

        public Program (string roomId) {
            this.roomId = roomId;
        }

        public void bindFetcher (ILiveFetcher fetcher) {
            this.fetcher = fetcher;
        }

        public void onStatusUpdate (bool isOn) {
            Debug.WriteLine ($"Status --> {(isOn ? "on" : "off")}");
        }

        public void onDurationUpdate (long duration, string timeText) {
            Debug.WriteLine ($"Downloading ..... {sizeText}[{timeText}]");
            ++durationUpdateTimes;
            if (durationUpdateTimes > 10) {
                durationUpdateTimes = 0;
                //roomId = rooms[new Random().Next(rooms.Length)];
                //downloader?.stop();
                //downloader?.start();
            }
        }

        public void onDownloadSizeUpdate (long totalSize, string sizeText) {
            this.sizeText = sizeText;
        }

        public void onBitRateUpdate (long bitRate, string bitRateText) {
            Debug.WriteLine ("BitRate ..... " + bitRateText);
        }

        public void onHotUpdate (long onlineCount) {
            fetcher.Logger.log (Level.Info, "Hot updated ..... " + onlineCount);
        }

        public void onLog (Level level, string message) {
            Debug.WriteLine (message, level.ToString ());
        }

        public void onMissionComplete (IMission mission) {
            //TODO Implement it
        }

        static void Main (string[] args) {
            var roomIds = new string[] { "5441", "469", "439", "305", "102" };
            var testOneId = roomIds[new Random ().Next (roomIds.Length - 1)];
            var app = new Program (testOneId);
            var fetcher = new LiveFetcher (app, string.Empty);
            app.bindFetcher (fetcher);
            fetcher.Logger.LogHandlers.add (app);
            fetcher.putExtra ("flv-needless", false);
            fetcher.LiveProgressBinders.add (app);
            fetcher.DanmakuHandlers.add (danmaku => {
                if (danmaku == null || danmaku.MsgType != MsgTypeEnum.Comment) return;
                Debug.WriteLine ($"\"{danmaku.UserName}\" : {danmaku.CommentText}");
            });
            fetcher.start ();
            ConsoleKeyInfo press;
            while ((press = Console.ReadKey ()).Key != ConsoleKey.Escape) { }
        }
    }
}