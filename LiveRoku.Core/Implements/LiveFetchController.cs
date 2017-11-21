namespace LiveRoku.Core {
    using LiveRoku.Base;
    using LiveRoku.Base.Logger;
    using LiveRoku.Core.Common;
    using LiveRoku.Core.Models;
    using LiveRoku.Core.Api;
    using LiveRoku.Core.Danmaku;
    using LiveRoku.Core.Download;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;

    public class PreferencesLite {
        public string Folder { get; set; }
        public string FileNameFormat { get; set; }
        public bool AutoStart { get; set; }
        public bool DanmakuRequire { get; set; }
        public bool VideoRequire { get; set; }
    }
    [SuppressMessage ("Microsoft.Performance", "CS4014")]
    public partial class LiveFetchController : IContext, IDisposable {
        public ILiveFetcher Fetcher => this;
        public IPreferences Preferences => this.pref;
        public ISettingsBase RuntimeExtra => extra;
        public bool IsRunning { get; private set; }//Value of if it's start download
        public bool? IsLiveOn => dmCarrier.IsLiveOn;//The live status
        public bool IsStreaming => actor.IsStreaming;//Result of it's downloading or not

        private readonly ISettings extra = new EasySettings ();
        private readonly CancellationManager mgr = new CancellationManager ();
        private readonly INetworkWatcher network = new NetworkWatcherProxy ();
        private readonly PreferencesLite prefCopy = new PreferencesLite();
        private readonly IPreferences pref; //Provide base parameters
        private readonly IWebApi rootAccessApi; //API access
        private readonly RoomDataLiteApi dataApi;
        private readonly DanmakuCarrier dmCarrier;
        private readonly IDownloadActor emptyActor;
        private readonly IDownloadActor dloadActor;
        private readonly int requestTimeout;
        private IDownloadActor actor;

        public LiveFetchController (IPreferences basicArgs, int requestTimeout) {
            var userAgent = basicArgs?.UserAgent;
            //Initialize access api
            this.rootAccessApi = new BiliApi (new StandardHttpClient {
                DefaultEncoding = System.Text.Encoding.UTF8,
                Timeout = TimeSpan.FromMilliseconds(requestTimeout)
            }, Logger, userAgent);
            this.dataApi = new RoomDataLiteApi(rootAccessApi, Logger);
            this.requestTimeout = requestTimeout;
            this.pref = basicArgs;
            //.............
            //Make chat/download worker
            //And subscribe base events for boardcasting
            //.............
            this.dmCarrier = new DanmakuCarrier(rootAccessApi, emitter, this) {
                LiveCommandRecv = type => actor.onLiveCommand(type, prefCopy, dataApi),
                PretreatDanmaku = dm => actor.onDanmaku(dm),
                InactiveTotally = checkIsNeedToGoToStop
            };
            this.emptyActor = new EmptyDownloadActor();
            this.dloadActor = new VideoDownloadActor(new LiveDownloadWorker(this, userAgent) {
                BitRateUpdated = br => emitter.boardcastBitRateUpdate(br, $"{br / 1000} Kbps"),
                DurationUpdated = d => emitter.boardcastDurationUpdate(d, SharedHelper.getFriendlyTime(d)),
                DownloadSizeUpdated = size => emitter.boardcastDownloadSizeUpdate(size, size.ToFileSize()),
                MissionCompleted = toWaitingOrStopAndBoardcastMission,
                Streaming = activeDmCarrierAndBoardcastStreaming
            }, isWorkModeAndLiveOn, requestTimeout, mgr, Logger);
            this.actor = this.emptyActor;
        }

        public LiveFetchController (IPreferences basicArgs):
            this (basicArgs, 20000) { }

        ~ LiveFetchController() {
            Dispose(false);
        }

        public void Dispose () {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing) {
            stop();
            dmCarrier.purgeEvents();
            emitter.emptyHandlers();
        }

        //-----------------------
        //--- Fetch room info ---
        //-----------------------
        public IRoomInfo getRoomInfo (bool refresh) {
            if (!IsRunning || refresh) {
                string shortId = pref.ShortRoomId;
                if (int.TryParse (shortId, out int roomId)) {
                    if (!shortId.Equals(dataApi.ShortRoomId)) {
                        dataApi.resetShortId(shortId);
                    }
                    refreshRoomInfo();
                }
            }
            return dataApi.RoomInfo;
        }

        //---------------------
        //--- Stop or start ---
        //---------------------
        public void stop (bool force = false) => stopImpl (force, false);

        public void start () {
            if (IsRunning) return;
            IsRunning = true;
            //Basic initialize
            network.assumeAvailability (true);
            network.attach (available => {
                Logger.log (Level.Info, $"Network Availability ->  {available}");
                if (!available) stopImpl (false, true);
                else if (!IsRunning) start ();
            });
            bool isVideoMode = pref.LocalVideoRequire;
            //Register linker
            actor = isVideoMode ? dloadActor : emptyActor;
            //reset state
            actor.onReset();
            dmCarrier.resetState ();
            //Basic parameters check
            //Get running parameters
            var roomIdText = pref.ShortRoomId;
            if (!int.TryParse(roomIdText, out int roomId) || roomId <= 0) {
                Logger.log(Level.Error, "Wrong id.");
                stop();
            } else if (string.IsNullOrEmpty(pref.StoreFolder) || string.IsNullOrEmpty(pref.StoreFileNameFormat)) {
                Logger.log(Level.Error, "Store location not valid.");
                stop();
            } else {
                //Copy parameters
                prefCopy.Folder = pref.StoreFolder;
                prefCopy.FileNameFormat = pref.StoreFileNameFormat;
                prefCopy.AutoStart = pref.AutoStart;
                prefCopy.VideoRequire = isVideoMode;
                prefCopy.DanmakuRequire = pref.LocalDanmakuRequire;
                //!
                dataApi.resetShortId(roomIdText);
                dataApi.IsShortIdTheRealId = pref.IsShortIdTheRealId;
                //Preparing signal
                this.RuntimeExtra.put("video-require", prefCopy.VideoRequire);
                this.RuntimeExtra.put("store-folder", prefCopy.Folder);
                emitter.boardcastPreparing(this);
                prepareDownload();
            }
        }


        //Re-get roominfo and raise event
        private IRoomInfo refreshRoomInfo() {
            var info = dataApi.fetchRoomInfo();
            actor.onRoomInfo(info);
            dmCarrier.syncStatusFrom(info);
            return info;
        }

        private void stopImpl (bool force, bool callByInternal) {
            Debug.WriteLine ("stopImpl invoke");
            if (!callByInternal /* so network watcher needless*/) {
                network.detach (); //detach now
            }
            mgr.cancelAll ();
            mgr.clear ();
            if (IsRunning) {
                IsRunning = false;
                dmCarrier.disconnect ();
                actor.stopAsync(force);
                Logger.log(Level.Info, "Fetch stopped.");
                emitter.boardcastStopped (this);
            }
        }

        //Prepare download after basic parameters checked
        private void prepareDownload () {
            //Prepare to start task
            //Cancel when no result back over five second
            //Prepare real roomId and flv url
            bool isUpdated = false;
            mgr.runOnlyOne ("fetch-url-realId", () => {
                if (!prefCopy.VideoRequire) {
                    Logger.log(Level.Info, $"No video mode");
                    isUpdated = dataApi.fetchRealId();
                } else {
                    isUpdated = dataApi.fetchRealIdAndUrl("prepare");
                }
            }, requestTimeout).ContinueWith (task => {
                mgr.runOnlyOne("fetch-start-impl", () => {
                    if (isUpdated/*means get args successful*/) {
                        mgr.runOnlyOne("fetch-room-info", () => {
                            refreshRoomInfo();
                        });
                        //All parameters ready
                        emitter.boardcastWaiting(this);
                        dmCarrier.connectAsync(dataApi.RealRoomId);
                        actor.onCallDownload(prefCopy, dataApi/*will test video require*/);
                    } else {
                        Logger.log(Level.Error, $"Get room detail fail, RealRoomId : {dataApi.RealRoomId}");
                        stop();
                    }
                });
            });
        }
        
        //Check status and boardcast mission complete event
        private void toWaitingOrStopAndBoardcastMission(IMission mission) {
            string debugInfo = mission == null ? string.Empty : $"from {mission.BeginTime}, to {mission.EndTime}, size: {mission?.RecordSize}";
            Debug.WriteLine("Mission complete. "+ debugInfo, "emitter");
            emitter.boardcastMissionComplete(mission);
            if (IsRunning/* to back to waiting status*/) {
                if (dmCarrier.IsChannelActive) {
                    emitter.boardcastWaiting(this);
                } else {
                    checkIsNeedToGoToStop();
                }
            }
        }

        private void activeDmCarrierAndBoardcastStreaming() {
            emitter.boardcastStreaming(this);
            mgr.runOnlyOne("ensure-carrier", () => {
                //Ensure carrier working
                //IsChannelActive will check in method implement
                dmCarrier.connectAsync(dataApi.RealRoomId);
            });
        }
        
        private void checkIsNeedToGoToStop() {
            if(IsRunning && !dmCarrier.IsChannelActive && !IsStreaming) {
                stop();
            }
        }


        //--------------------
        //--- Help method ----
        //--------------------
        private bool isWorkModeAndLiveOn() {
            return IsRunning && dmCarrier.IsLiveOn == true;
        }

        private bool isValueTrue (IDictionary<string, object> dict, string key) {
            return dict.ContainsKey (key) && dict[key] is bool && ((bool) dict[key]);
        }

        //var dict = new Dictionary<T1, T2> { { key, value } };
    }

}