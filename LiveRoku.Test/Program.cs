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
        public string UserAgent { get; set; }
        public bool DownloadDanmaku { get; set; } = true;
        public bool AutoStart { get; set; } = true;
    }

    class Program : ILogHandler, IFetchArgsHost, ILiveProgressBinder {
        public string RoomId => Args.RoomId;
        public string Folder => Args.Folder;
        public string FileFormat => Args.FileFormat;
        public string UserAgent => Args.UserAgent;
        public bool DownloadDanmaku => Args.DownloadDanmaku;
        public bool AutoStart => Args.AutoStart;
        private ILiveFetcher fetcher;
        private string sizeText;
        private int durationUpdateTimes = 0;
        public ArgsModel Args { get; private set; }

        public Program (ArgsModel args) {
            this.Args = args;
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

        static void runSafely (Action doWhat) {
            try {
                doWhat?.Invoke ();
            } catch (Exception e) {
                Debug.WriteLine (e.ToString ());
            }
        }

        static void Main (string[] args) {
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
            //Make argHost
            Program argsHost = null;

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
            Parallel.ForEach (ctx.Plugins, plugin => {
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
            //fetcher.Extra.put ("cancel-flv", true);
            fetcher.DanmakuHandlers.add (danmaku => {
                if (danmaku == null || danmaku.MsgType != MsgTypeEnum.Comment) return;
                Debug.WriteLine ($"\"{danmaku.UserName}\" : {danmaku.CommentText}");
            });
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
            Debug.WriteLine ($"Save settings used {sw.ElapsedMilliseconds}");
        }
    }
}