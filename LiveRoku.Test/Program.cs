using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using LiveRoku.Base;
using LiveRoku.Base.Logger;
using LiveRoku.Loader;

namespace LiveRoku.Test {

    class ArgsModel {
        public string RoomId { get; set; }
        public string Folder { get; set; } = "C:/mnt/hd1/station/bilibili/test/testtemp";
        public string FileFormat { get; set; } = "{roomId}-{Y}-{M}-{d}-{H}-{m}-{s}.flv";
        public bool DanmakuRequire { get; set; } = true;
        public bool VideoRequire { get; set; } = true;
        public bool AutoStart { get; set; } = true;
        public string UserAgent { get; set; }
    }

    class Program : LiveResolverBase, ILogHandler, IPreferences {

        public string ShortRoomId => Args.RoomId;
        public string RealRoomId => null;
        public bool IsShortIdTheRealId => false;

        public string StoreFolder => Args.Folder;
        public string StoreFileNameFormat => Args.FileFormat;

        public bool LocalDanmakuRequire => Args.DanmakuRequire;
        public bool LocalVideoRequire => Args.VideoRequire;
        public bool AutoStart => Args.AutoStart;
        public string UserAgent => Args.UserAgent;

        private ILiveFetcher fetcher;
        private string sizeText;
        private int durationUpdateTimes = 0;
        public ArgsModel Args { get; private set; }
        public ISettingsBase Extra { get; } = new EasySettings();

        public Program (ArgsModel args) {
            this.Args = args;
        }

        public void bindFetcher (ILiveFetcher fetcher) {
            this.fetcher = fetcher;
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

        public override void onHotUpdateByDanmaku (long onlineCount) {
            fetcher.Logger.log (Level.Info, "Hot updated ..... " + onlineCount);
        }

        public override void onLog (Level level, string message) {
            Debug.WriteLine (message, level.ToString ());
        }

        public override void onMissionComplete (IMission mission) {
            //TODO Implement it
        }

        public override void onPreparing() {
            Debug.WriteLine("base.onPreparing()", "program");
        }

        public override void onWaiting() {
            Debug.WriteLine("base.onWaiting()", "program");
        }
        public override void onStopped() {
            Debug.WriteLine("base.onStopped()", "program");
        }
        public override void onStreaming() {
            Debug.WriteLine("base.onStreaming()", "program");
        }
        public override void onDanmakuReceive(DanmakuModel danmaku) {
            if (danmaku == null || danmaku.MsgType != MsgTypeEnum.Comment)
                return;
            Debug.WriteLine($"\"{danmaku.UserName}\" : {danmaku.CommentText}");
        }

        static void runSafely (Action doWhat) {
            try {
                doWhat?.Invoke ();
            } catch (Exception e) {
                Debug.WriteLine (e.ToString ());
            }
        }

        static void Main (string[] args) {
            var logTracker = new LogDateTimeTraceListener(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug.txt"));
            Trace.Listeners.Add(logTracker);
            Trace.AutoFlush = true;

            //UIThread for plugin of which need to show WPF ui element
            var uiThread = new Thread (() => {
                var app = new Application();
                app.Run ();
                //System.Windows.Threading.Dispatcher.Run ();
            });
            uiThread.SetApartmentState (ApartmentState.STA);
            uiThread.IsBackground = true;
            uiThread.Start ();

            //Choose room id
            var roomIds = new string[] { "5441", "469", "439", "305", "102", "183", "118", "501", "379", "131", "413" };
            var testOneId = roomIds[new Random ().Next (roomIds.Length - 1)];
            var bridge = new CoreBridge();
            bridge.setupContext(isCoreLoaded => {
                Console.WriteLine("Load core context.");
            }, e => {
                Console.WriteLine("Load context make a exception. " + e.ToString());
            }).Wait();
            if (bridge.IsCoreLoaded) {
                if (string.IsNullOrEmpty(bridge.ShortRoomId)) {
                    bridge.ShortRoomId = testOneId;
                }
                bridge.invokeStart();
                //Waitting for exit
                ConsoleKeyInfo press;
                while ((press = Console.ReadKey()).Key != ConsoleKey.Escape) { }
                bridge.invokeStop();
                bridge.detachAndSave();
            }
            //Make argHost
            /*Program argsHost = null;

            var sw = new Stopwatch ();
            sw.Start ();
            //Load core and plugins
            var bootstrap = new LoadManager (AppDomain.CurrentDomain.BaseDirectory);
            LoadContext ctx = null;
            runSafely (() => {
                var ctxBase = bootstrap.initCtxBase ();
                var mArgs = ctxBase.AppLocalData.getAppSettings ().get ("Args", new ArgsModel ());
                mArgs.RoomId = mArgs.RoomId ?? testOneId;
                argsHost = new Program (mArgs);
                if ((ctx = bootstrap.create (argsHost)) == null) {
                    Console.WriteLine ("Cannot load context.");
                    Console.ReadKey ();
                    Environment.Exit (-1);
                }
            });
            Debug.WriteLine ($"Load context used {sw.ElapsedMilliseconds}");
            sw.Restart ();

            //Init plugins
            ctx.Plugins.ForEach (plugin => {
                runSafely (() => plugin.onInitialize (ctx.AppLocalData.getAppSettings ()));
                runSafely (() => plugin.onAttach (ctx));
            });
            Debug.WriteLine ($"Load plugins used {sw.ElapsedMilliseconds}");
            sw.Stop ();

            //Attach fetcher
            var fetcher = ctx.Fetcher;
            argsHost.bindFetcher (fetcher);
            fetcher.Logger.LogHandlers.add (argsHost);
            fetcher.LiveProgressBinders.add (argsHost);
            fetcher.StatusBinders.add(argsHost);
            //fetcher.Extra.put ("cancel-flv", true);
            fetcher.DanmakuHandlers.add (argsHost);
            fetcher.start ();
            //Waitting for exit
            ConsoleKeyInfo press;
            while ((press = Console.ReadKey ()).Key != ConsoleKey.Escape) { }

            //Detach
            fetcher.stop();
            fetcher.Dispose();
            sw.Start ();
            ctx.AppLocalData.getAppSettings ().put ("Args", argsHost.Args);
            Parallel.ForEach (ctx.Plugins, plugin => {
                runSafely (() => plugin.onDetach (ctx));
            });
            Debug.WriteLine ($"Detach plugins used {sw.ElapsedMilliseconds}");
            sw.Restart ();
            ctx.saveAppData ();
            Debug.WriteLine ($"Save settings used {sw.ElapsedMilliseconds}");*/
        }
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
}