using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using LiveRoku.Base;
using LiveRoku.Base.Logger;
using LiveRoku.Loader;

namespace LiveRoku.Test {
    
    class Program : LiveResolverBase{
        private int durationUpdateTimes;
        private string sizeText;
        private readonly ILogger logger;
        public Program(ILogger logger) {
            this.logger = logger;
        }

        public override void onLiveStatusUpdate (bool isOn) {
            Debug.WriteLine ($"Status --> {(isOn ? "on" : "off")}", "program");
        }

        public override void onDurationUpdate (long duration, string timeText) {
            Debug.WriteLine ($"Downloading ..... {sizeText}[{timeText}]", "program");
            ++durationUpdateTimes;
            if (durationUpdateTimes > 10) {
                durationUpdateTimes = 0;
                //roomId = rooms[new Random().Next(rooms.Length)];
                //downloader?.stop();
                //downloader?.start();
            }
        }

        public override void onDownloadSizeUpdate (long totalSize, string sizeText) {
            this.sizeText = sizeText;
        }

        public override void onBitRateUpdate (long bitRate, string bitRateText) {
            Debug.WriteLine ("BitRate ..... " + bitRateText, "program");
        }
        
        public override void onLog (Level level, string message) {
            //Do nothing
        }

        public override void onMissionComplete (IMission mission) {
            //TODO Implement it
        }

        public override void onPreparing(IContext ctx) {
            Debug.WriteLine("base.onPreparing()", "program");
        }
        public override void onWaiting(IContext ctx) {
            Debug.WriteLine("base.onWaiting()", "program");
        }
        public override void onStopped(IContext ctx) {
            Debug.WriteLine("base.onStopped()", "program");
        }
        public override void onStreaming(IContext ctx) {
            Debug.WriteLine("base.onStreaming()", "program");
        }
        public override void onDanmakuReceive(DanmakuModel danmaku) {
            if (danmaku == null || danmaku.MsgType != MsgTypeEnum.Comment)
                return;
            Debug.WriteLine($"\"{danmaku.UserName}\" : {danmaku.CommentText}", "danmaku");
        }
        
        internal class LogDateTimeTraceListener : TextWriterTraceListener {
            private object locker = new object();
            public LogDateTimeTraceListener(string path) : base(path) { }

            public override void WriteLine(string message, string category) {
                lock (locker)
                    base.WriteLine(string.Format("[{0:yyyy-MM-dd HH:mm:ss.fff}] [{1}]: {2}", DateTime.Now, category, message.Replace("\n", Environment.NewLine)));
            }

            public override void WriteLine(string message) {
                lock (locker)
                    base.WriteLine(string.Format("[{0:yyyy-MM-dd HH:mm:ss.fff}] : {1}", DateTime.Now, message.Replace("\n", Environment.NewLine)));
            }
        }

        static void Main (string[] args) {
            var logTracker = new LogDateTimeTraceListener(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug.txt"));
            Trace.Listeners.Add(logTracker);
            Trace.AutoFlush = true;
            Debug.WriteLine("--------------------------", "block");
            Debug.WriteLine("---------  begin ---------", "block");
            Debug.WriteLine("--------------------------", "block");

            //UIThread for plugin of which need to show WPF ui element
            var uiThread = new Thread (() => {
                var app = new Application();
                app.Run ();
                //System.Windows.Threading.Dispatcher.Run ();
            });
            uiThread.SetApartmentState (ApartmentState.STA);
            uiThread.IsBackground = true;
            uiThread.Start ();

            var bridge = new CoreBridge();
            bridge.setupContext(isCoreLoaded => {
                Console.WriteLine("Core context loaded.");
            }, e => {
                Console.WriteLine("Load context make a exception. " + e.ToString());
            }).Wait();
            if (bridge.IsCoreLoaded) {
                var fetcher = bridge.Context.Fetcher;
                var monitor = new Program(fetcher.Logger);
                fetcher.DanmakuHandlers.add(monitor);
                fetcher.StatusBinders.add(monitor);
                fetcher.DownloadProgressBinders.add(monitor);
                fetcher.Logger.LogHandlers.add(monitor);
                //check pref
                if (string.IsNullOrEmpty(bridge.ShortRoomId)) {
                    //Choose room id
                    var roomIds = new string[] { "5441", "469", "439", "305", "102", "183", "118", "501", "379", "131", "413" };
                    bridge.ShortRoomId = roomIds[new Random().Next(roomIds.Length - 1)];
                }
                bridge.invokeStart();

                //Waitting for exit
                ConsoleKeyInfo press;
                while ((press = Console.ReadKey()).Key != ConsoleKey.Escape) { }
                bridge.detachAndSave();
            }
        }
    }
}