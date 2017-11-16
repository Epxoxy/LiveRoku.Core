namespace LiveRoku.Loader {
    using LiveRoku.Base;
    using LiveRoku.Base.Logger;
    using PropertyChanged;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading.Tasks;
    
    public enum ProcessState {
        Stopped,
        Preparing,
        Waiting,
        Streaming
    }

    public class BasicPreferences {
        public string LatestRoomId { get; private set; }
        public bool IsLatestTheRealId { get; private set; }
        //control
        public bool AutoStart { get; set; } = true;
        public bool LocalDanmakuRequire { get; set; } = true;
        public bool LocalVideoRequire { get; set; } = true;
        //store
        public string StoreFolder { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        public string StoreFileNameFormat { get; set; } = "{roomId}-{Y}-{M}-{d}-{H}-{m}-{s}.flv";

        public Dictionary<string, bool> RecentRooms { get; set; } = new Dictionary<string, bool>();
        public Dictionary<string, object> Extras { get; set; } = new Dictionary<string, object>();

        public void addLatestRoom(string roomId, bool isTheRealId) {
            this.LatestRoomId = roomId;
            if (this.RecentRooms == null)
                this.RecentRooms = new Dictionary<string, bool>();
            if (!this.RecentRooms.ContainsKey(roomId)) {
                this.RecentRooms.Add(roomId, isTheRealId);
            } else {
                this.RecentRooms[roomId] = isTheRealId;
            }
        }
    }
    
    //TODO Implement MVVM
    //TODO Move command from UI's event handlers
    [AddINotifyPropertyChangedInterface]
    public class CoreBridge : LiveResolverBase, IPreferences, ILogHandler {
        public string ShortRoomId { get; set; }
        public bool IsShortIdTheRealId { get; set; } = false;
        public string StoreFolder { get; set; }
        public string StoreFileNameFormat { get; set; }
        public bool LocalDanmakuRequire { get; set; } = true;
        public bool LocalVideoRequire { get; set; } = true;
        public bool AutoStart { get; set; } = true;
        public string UserAgent { get; set; } = string.Empty;
        public BasicPreferences LocalPreferences { get; private set; }
        [DoNotNotify]
        public ISettingsBase Extra { get; set; }

        public string VideoFullPathFormat { get; set; }
        //real-time data
        public long Popularity { get; private set; }
        public string BitRate { get; private set; }
        public string ReceiveSize { get; private set; }
        public string Duration { get; private set; }
        //basic control
        public bool IsStateChangeable { get; private set; } = true;
        public bool IsPreferencesEditable { get; private set; } = true;
        //current state
        public IRoomInfo CurrentRoomInfo { get; private set; }
        public ProcessState CurrentState { get; private set; }
        public bool IsLiveOn { get; private set; }
        public bool IsCoreLoaded { get; private set; }
        public bool IsContextLoaded => ctx != null;

        [DoNotNotify]
        private ILiveFetcher Fetcher => ctx?.Fetcher;
        private ModuleContext ctx;
        private LoadManager mgr;
        private volatile bool isLoading = false;

        public Task<bool> setupContext(Action<bool> onCoreLoaded, Action<Exception> wayErrorTips) {
            if (isLoading)
                return Task.FromResult(false);
            return Task.Run(() => {
                isLoading = true;
                mgr = mgr ?? new LoadManager(AppDomain.CurrentDomain.BaseDirectory);
                ModuleContextLoader loader = null;
                //Load core base
                runSafely(() => {
                    if((loader = mgr.generateLoader()) != null) {
                        this.restoreFrom(loader.BaseContext.AppLocalData.getAppSettings());
                    }
                }, wayErrorTips);
                this.IsCoreLoaded = loader != null;
                //Init core context
                if (IsCoreLoaded) {
                    onCoreLoaded?.Invoke(true);
                    runSafely(() => {
                        ctx = loader.create(this);
                    }, wayErrorTips);
                }
                if (ctx != null) {
                    //Register handlers of this to fetcher
                    ctx.Fetcher.Logger.LogHandlers.add(this);
                    ctx.Fetcher.LiveProgressBinders.add(this);
                    ctx.Fetcher.DanmakuHandlers.add(this);
                    ctx.Fetcher.StatusBinders.add(this);
                    ctx.Plugins.ForEach(plugin => {
                        runSafely(() => {
                            plugin.onInitialize(ctx.AppLocalData.getAppSettings());
                            ctx.Fetcher.Logger.log(Level.Info, $"{plugin.GetType().Name} Loaded.");
                        });
                        runSafely(() => {
                            plugin.onAttach(ctx);
                            ctx.Fetcher.Logger.log(Level.Info, $"{plugin.GetType().Name} Attach.");
                        });
                    });
                }
                isLoading = false;
                return ctx != null;
            });
        }

        public void detachAndSave() {
            if (ctx != null) {
                ctx.Fetcher?.stop();
                ctx.Fetcher?.Dispose();
                ctx.Plugins.ForEach(plugin => {
                    runSafely(() => plugin.onDetach(ctx));
                });
                storeTo(ctx.AppLocalData.getAppSettings());
                ctx.saveAppData();
            }
        }

        //settings operation
        private void restoreFrom(ISettings settings) {
            var previous = settings.get<BasicPreferences>("Prefs", null);
            if (previous == null)
                return;
            this.ShortRoomId = previous.LatestRoomId;
            this.IsShortIdTheRealId = previous.IsLatestTheRealId;
            this.StoreFolder = previous.StoreFolder;
            this.StoreFileNameFormat = previous.StoreFileNameFormat;
            this.VideoFullPathFormat = System.IO.Path.Combine(StoreFolder, StoreFileNameFormat);
            this.LocalDanmakuRequire = previous.LocalDanmakuRequire;
            this.LocalVideoRequire = previous.LocalVideoRequire;
            this.AutoStart = previous.AutoStart;
            this.LocalPreferences = previous;
        }

        //settings operation
        private void storeTo(ISettings settings) {
            var newPref = new BasicPreferences {
                StoreFolder = this.StoreFolder,
                StoreFileNameFormat = this.StoreFileNameFormat,
                LocalDanmakuRequire = this.LocalDanmakuRequire,
                LocalVideoRequire = this.LocalVideoRequire,
                AutoStart = this.AutoStart
            };
            if (int.TryParse(ShortRoomId, out int roomId)) {
                newPref.addLatestRoom(ShortRoomId, IsShortIdTheRealId);
            }
            settings.put("Prefs", newPref);
        }

        public Task updateRoomInfo() {
            return Task.Run(() => {
                this.CurrentRoomInfo = this.Fetcher?.getRoomInfo(true);
            });
        }

        public void switchStartAndStop() {
            if (Fetcher == null) return;
            if (Fetcher.IsRunning) Fetcher.stop();
            else Fetcher.start();
        }

        //----------------------------------------------
        //--------------- ILogHandler ------------------
        //----------------------------------------------
        public override void onLog(Level level, string message) {
            string info = $"[{level}] {message}\n";
            Debug.WriteLine(message, level.ToString());
        }

        //----------------------------------------------
        //--------------- IStatusBinder ----------------
        //----------------------------------------------
        public override void onPreparing() {
            CurrentState = ProcessState.Preparing;
            IsPreferencesEditable = false;
            IsStateChangeable = false;
        }

        public override void onWaiting() {
            CurrentState = ProcessState.Waiting;
            IsPreferencesEditable = false;
            IsStateChangeable = true;
        }

        public override void onStreaming() {
            CurrentState = ProcessState.Streaming;
            IsPreferencesEditable = false;
            IsStateChangeable = true;
        }

        public override void onStopped() {
            CurrentState = ProcessState.Stopped;
            IsPreferencesEditable = true;
            IsStateChangeable = true;
        }

        //----------------------------------------------
        //--------------- IDanmakuResolver -------------
        //----------------------------------------------
        public override void onLiveStatusUpdate(bool isOn) {
            IsLiveOn = isOn;
        }

        public override void onHotUpdateByDanmaku(long popularity) {
            Popularity = popularity;
        }

        //----------------------------------------------
        //--------------- IDownloadProgressBinder ------
        //----------------------------------------------
        public override void onDurationUpdate(long duration, string timeText) {
            Duration = timeText;
        }

        public override void onDownloadSizeUpdate(long size, string sizeText) {
            ReceiveSize = sizeText;
        }

        public override void onBitRateUpdate(long bitRate, string bitRateText) {
            BitRate = bitRateText;
        }

        //helpers
        public static void runSafely(Action doWhat) {
            try {
                doWhat?.Invoke();
            } catch (Exception e) {
                Debug.WriteLine(e.ToString());
            }
        }

        public static void runSafely(Action doWhat, Action<Exception> onError) {
            try {
                doWhat?.Invoke();
            } catch (Exception e) {
                onError.Invoke(e);
            }
        }

    }

    //TODO Delete it
    public class Bindable : INotifyPropertyChanged {
        private Dictionary<string, object> properties = new Dictionary<string, object>();

        protected T Get<T>([CallerMemberName] string name = null) {
            Debug.Assert(name != null, "name != null");
            object value = null;
            if (properties.TryGetValue(name, out value))
                return value == null ? default(T) : (T)value;
            return default(T);
        }

        protected void Set<T>(T value, [CallerMemberName] string name = null) {
            Debug.Assert(name != null, "name != null");
            if (Equals(value, Get<T>(name)))
                return;
            properties[name] = value;
            OnPropertyChanged(name);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    }
}
