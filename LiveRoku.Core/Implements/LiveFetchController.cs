namespace LiveRoku.Core {
    using LiveRoku.Base;
    using LiveRoku.Base.Logger;
    using LiveRoku.Core.Common;
    using LiveRoku.Core.Models;
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
    public partial class LiveFetchController : IDisposable {
        public ISettingsBase Extra => extra;
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
                PretreatDanmaku = dm => actor.onDanmaku(dm)
            };
            this.emptyActor = new EmptyDownloadActor();
            this.dloadActor = new VideoDownloadActor(new LiveDownloadWorker(this, userAgent) {
                BitRateUpdated = bitRate => emitter.boardcastBitRateUpdate(bitRate, $"{bitRate / 1000} Kbps"),
                DurationUpdated = duration => emitter.boardcastDurationUpdate(duration, SharedHelper.getFriendlyTime(duration)),
                DownloadSizeUpdated = totalBytes => emitter.boardcastDownloadSizeUpdate(totalBytes, totalBytes.ToFileSize()),
                MissionCompleted = mission => {
                    emitter.boardcastMissionComplete(mission);
                    if (IsRunning/* to back to waiting status*/) {
                        emitter.boardcastWaiting();
                    }
                },
                Streaming = () => /* ensure carrier working*/ {
                    mgr.runOnlyOne("ensure-carrier", () => {
                        //IsChannelActive will check in method implement
                        dmCarrier.connectAsync(dataApi.RealRoomId);
                    });
                    emitter.boardcastStreaming();
                }
            }, isWorkModeAndLiveOn, requestTimeout, mgr, Logger);
            this.actor = this.emptyActor;
        }

        public LiveFetchController (IPreferences basicArgs):
            this (basicArgs, 20000) { }

        public void Dispose () {
            stop ();
            dmCarrier.purgeEvents ();
            emitter.emptyHandlers ();
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
            //Basic initialize
            network.assumeAvailability (true);
            network.attach (available => {
                Logger.log (Level.Info, $"Network Availability ->  {available}");
                if (!available) stopImpl (false, true);
                else if (!IsRunning) start ();
            });
            IsRunning = true;
            bool isVideoMode = pref.VideoRequire;
            //Register linker
            actor = isVideoMode ? dloadActor : emptyActor;
            //reset state
            actor.onReset();
            dmCarrier.resetState ();
            //Basic parameters check
            //Get running parameters
            var roomIdText = pref.ShortRoomId;
            if (!int.TryParse (roomIdText, out int roomId) || roomId <= 0) {
                Logger.log (Level.Error, "Wrong id.");
                stop ();
            } else {
                //Copy parameters
                prefCopy.Folder = pref.Folder;
                prefCopy.FileNameFormat = pref.FileFormat;
                prefCopy.AutoStart = pref.AutoStart;
                prefCopy.VideoRequire = isVideoMode;
                prefCopy.DanmakuRequire = pref.DanmakuRequire;
                //!
                dataApi.resetShortId(roomIdText);
                dataApi.IsShortIdTheRealId = pref.IsShortIdTheRealId;
                //Preparing signal
                this.Extra.put("video-require", prefCopy.VideoRequire);
                emitter.boardcastPreparing();
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
                emitter.boardcastStopped ();
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
                        emitter.boardcastWaiting();
                        dmCarrier.connectAsync(dataApi.RealRoomId);
                        actor.onCallDownload(prefCopy, dataApi/*will test video require*/);
                    } else {
                        Logger.log(Level.Error, $"Get room detail fail, RealRoomId : {dataApi.RealRoomId}");
                        stop();
                    }
                });
            });
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