using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using LiveRoku.Base;

namespace LiveRoku.Core {
    internal class DanmakuCenter {
        public bool IsConnected => IsActive;
        public bool IsLiveOn => isLiveOn;
        private bool IsActive => transform?.isActive () == true;
        private readonly ILiveEventEmitter em;
        private readonly BiliApi biliApi; //API access
        private readonly EventSubmitHandler events;
        private readonly ReconnectArgs reconnect = new ReconnectArgs ();
        private readonly object oneTransform = new object ();
        private CancellationTokenSource timeout;
        private bool isLiveOn;
        private bool isEnabled;
        private int realRoomId;
        private NetResolverLite transform;
        class ReconnectArgs {
            public long DelayReconnectMs { get; set; } = 500;
            public int RetryTimes { get; set; }
            public int MaxRetryTimes { get; set; } = 10;
            public bool canRetry () => RetryTimes < MaxRetryTimes;
            public void reset () {
                DelayReconnectMs = 500;
                RetryTimes = 0;
            }
        }

        public DanmakuCenter (ILiveEventEmitter em, BiliApi biliApi) {
            this.em = em;
            this.biliApi = biliApi;
            //Initialize Downloaders
            this.events = new EventSubmitHandler ();
            //Subscribe events
            this.events.OnLog = msg => em.Logger.log (Level.Info, msg);
            this.events.DanmakuReceived = emitDanmaku;
            this.events.HotUpdated = em.onHotUpdate;
            this.events.Connected = onConnected;
            this.events.Closed = reconnectOnError;
        }

        public void resetState () {
            isLiveOn = false;
            reconnect.reset ();
            if (IsActive) {
                disconnect ();
            }
        }

        public void purgeEvents () => events.purgeEvents ();

        public void connect (int realRoomId) {
            this.isEnabled = true;
            this.realRoomId = realRoomId;
            if (!IsActive) {
                connectByApi (biliApi, realRoomId);
            }
        }

        private bool connectByApi (BiliApi biliApi, int realRoomId) {
            BiliApi.ServerBean bean = null;
            if (biliApi.tryGetValidDmServerBean (realRoomId.ToString (), out bean)) {
                em.Logger.log (Level.Info, "Trying to connect to danmaku server.");
                activeTransform (bean.Host, bean.Port, realRoomId);
                return true;
            } else {
                em.Logger.log (Level.Error, "Cannot get valid server address and port.");
                return false;
            }
        }

        public bool activeTransform (String host, int port, int realRoomId) {
            lock (oneTransform) {
                if (IsActive) return false;
                //............
                transform?.Resolvers.clear ();
                transform = new NetResolverLite ();
                transform.Resolvers.addLast (new KeepAliveHandler (realRoomId));
                transform.Resolvers.addLast (new UnpackHandler ());
                transform.Resolvers.addLast (events);
                transform.connectAsync (host, port);
                return true;
            }
        }

        public void disconnect () {
            this.isEnabled = false;
            //close connection
            if (transform?.isActive () == true) {
                var temp = transform;
                temp.close ();
                temp.Resolvers.clear ();
                temp = null;
            }
            if (timeout?.Token.CanBeCanceled == true) {
                timeout.Cancel ();
            }
        }

        private void onConnected () {
            em.Logger.log (Level.Info, $"Connect to danmaku server ok.");
            reconnect.reset ();
        }

        private void emitDanmaku (DanmakuModel danmaku) {
            if (!isEnabled /*May not come here*/ ) return;
            findLiveStatusFrom (danmaku);
            em.danmakuRecv (danmaku);
        }

        private void findLiveStatusFrom (DanmakuModel danmaku) {
            if (MsgTypeEnum.LiveEnd == danmaku.MsgType) {
                //Update live status
                updateToLiveStatus (false);
                em.Logger.log (Level.Info, "Message received : Live End.");
            } else if (MsgTypeEnum.LiveStart == danmaku.MsgType) {
                //Update live status
                updateToLiveStatus (true);
                em.Logger.log (Level.Info, "Message received : Live Start.");
            }
        }

        private void updateToLiveStatus (bool isLiveOn, bool raiseEvent = true) {
            if (this.isLiveOn != isLiveOn) {
                this.isLiveOn = isLiveOn;
                if (raiseEvent) {
                    em.onStatusUpdate (isLiveOn);
                }
            }
        }

        private async void reconnectOnError (Exception e) {
            //TODO something here
            em.Logger.log (Level.Error, e?.Message);
            if (timeout?.Token.CanBeCanceled == true) {
                timeout.Cancel ();
            }
            if (!isEnabled) { //donnot reconnect when download stopped.
                return;
            }
            if (!reconnect.canRetry ()) {
                em.Logger.log (Level.Error, "Retry time more than the max.");
                return;
            }
            //set cancellation and start task.
            bool connectionOK = false;
            long used = 3000;
            timeout = new CancellationTokenSource (3000);
            Task.Run (() => {
                var sw = Stopwatch.StartNew ();
                connectionOK = SharedHelper.checkCanConnect ("live.bilibili.com");
                sw.Stop ();
                used = sw.ElapsedMilliseconds;
            }, timeout.Token).Wait ();
            var delay = reconnect.DelayReconnectMs - used;
            if (delay > 0) {
                await Task.Delay (TimeSpan.FromMilliseconds (delay));
                if (!isEnabled) return;
                em.Logger.log (Level.Info, $"Trying to reconnect to danmaku server after {(delay) / (double) 1000}s");
            }
            //increase delay
            reconnect.DelayReconnectMs += (connectionOK ? 1000 : reconnect.RetryTimes * 2000);
            reconnect.RetryTimes++;
            connectByApi (biliApi, realRoomId);
        }

    }
}