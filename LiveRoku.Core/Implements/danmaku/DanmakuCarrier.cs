namespace LiveRoku.Core {
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using LiveRoku.Base;
    using LiveRoku.Base.Logger;
    internal class DanmakuCarrier {
        public bool IsChannelActive => transform?.isActive() == true;
        public bool IsLiveOn { get; private set; }
        public Action<long> HotUpdated { get; set; }
        public Action<DanmakuModel> DanmakuRecv { get; set; }
        public Action<bool> LiveStatusUpdated { get; set; }
        //private readonly
        private readonly ILogger logger;
        private readonly BiliApi biliApi; //API access
        private readonly EventSubmitHandler events;
        private readonly ReconnectArgs reconnect = new ReconnectArgs ();
        private readonly object keepOneTransform = new object ();
        //private
        private CancellationTokenSource timeout;
        private bool isEnabled;
        private int realRoomId;
        private NetResolverLite transform;
        private class ReconnectArgs {
            public long DelayReconnectMs { get; set; } = 500;
            public int RetryTimes { get; set; }
            public int MaxRetryTimes { get; set; } = 10;
            public bool canRetry () => RetryTimes < MaxRetryTimes;
            public void reset () {
                DelayReconnectMs = 500;
                RetryTimes = 0;
            }
        }

        public DanmakuCarrier (ILogger logger, BiliApi biliApi) {
            this.logger = logger;
            this.biliApi = biliApi;
            //Initialize Downloaders
            this.events = new EventSubmitHandler {
                OnException = e => logger.log(Level.Info,
                $"chat transform exception : {e?.Message}"),
                OnMessage = emitDanmaku,
                HotUpdated = updateHot,
                Active = onActive,
                InActive = reconnectIfError
            };
            //Subscribe events
        }

        public void resetState () {
            IsLiveOn = false;
            reconnect.reset ();
            if (IsChannelActive) {
                disconnect ();
            }
        }

        public void purgeEvents () => events.purgeEvents ();
        
        public void disconnect () {
            this.isEnabled = false;
            //close connection
            if (transform?.isActive () == true) {
                var temp = transform;
                transform = null;
                temp.close();
                temp.Resolvers.clear();
                temp = null;
            }
            if (timeout?.Token.CanBeCanceled == true) {
                timeout.Cancel();
            }
        }

        public void connect (int realRoomId) {
            this.isEnabled = true;
            this.realRoomId = realRoomId;
            if (!IsChannelActive) {
                connectByApi (biliApi, realRoomId);
            }
        }

        private bool connectByApi (BiliApi biliApi, int realRoomId) {
            if (biliApi.tryGetValidDmServerBean (realRoomId.ToString (), out BiliApi.ServerBean bean)) {
                logger.log (Level.Info, "Trying to connect to danmaku server.");
                activeTransform (bean.Host, bean.Port, realRoomId);
                return true;
            } else {
                logger.log (Level.Error, "Cannot get valid server address and port.");
                return false;
            }
        }

        private bool activeTransform (String host, int port, int realRoomId) {
            lock (keepOneTransform) {
                if (IsChannelActive) return false;
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

        private void onActive () {
            logger.log (Level.Info, $"Connect to danmaku server ok.");
            reconnect.reset ();
        }

        private void emitDanmaku (DanmakuModel danmaku) {
            if (!isEnabled /*May not come here*/ ) return;
            findLiveStatusFrom (danmaku);
            DanmakuRecv?.Invoke(danmaku);
        }

        private void findLiveStatusFrom (DanmakuModel danmaku) {
            if (MsgTypeEnum.LiveEnd == danmaku.MsgType) {
                //Update live status
                updateToLiveStatus (false);
                logger.log (Level.Info, "Message received : Live End.");
            } else if (MsgTypeEnum.LiveStart == danmaku.MsgType) {
                //Update live status
                updateToLiveStatus (true);
                logger.log (Level.Info, "Message received : Live Start.");
            }
        }

        private void updateToLiveStatus (bool isLiveOn, bool raiseEvent = true) {
            if (this.IsLiveOn != isLiveOn) {
                this.IsLiveOn = isLiveOn;
                if (raiseEvent) {
                    LiveStatusUpdated?.Invoke(isLiveOn);
                }
            }
        }

        private void updateHot(long popularity) {
            HotUpdated?.Invoke(popularity);
        }

        private async void reconnectIfError (Exception e) {
            //TODO something here
            if(e!=null){
                logger.log (Level.Error, e.Message);
            }
            if (timeout?.Token.CanBeCanceled == true) {
                timeout.Cancel ();
            }
            if (!isEnabled) { //donnot reconnect when download stopped.
                return;
            }
            if (!reconnect.canRetry ()) {
                logger.log (Level.Error, "Retry time more than the max.");
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
                logger.log (Level.Info, $"Trying to reconnect to danmaku server after {(delay) / (double) 1000}s");
            }
            //increase delay
            reconnect.DelayReconnectMs += (connectionOK ? 1000 : reconnect.RetryTimes * 2000);
            reconnect.RetryTimes++;
            connectByApi (biliApi, realRoomId);
        }

    }
}