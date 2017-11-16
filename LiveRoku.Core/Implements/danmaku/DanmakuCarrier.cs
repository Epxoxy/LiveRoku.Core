namespace LiveRoku.Core {
    using LiveRoku.Base;
    using LiveRoku.Base.Logger;
    using LiveRoku.Core.Common;
    using LiveRoku.Core.Models;
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    internal class DanmakuCarrier {
        public bool IsChannelActive => transform?.isActive() == true;
        public bool? IsLiveOn { get; private set; }
        public Action<MsgTypeEnum> LiveCommandRecv { get; set; }
        public Action<DanmakuModel> PretreatDanmaku { get; set; }
        //private readonly
        private readonly ILogger logger;
        private readonly IWebApi accessApi; //API access
        private readonly EventSubmitHandler emitter;
        private readonly IDanmakuResolver resolver;
        private readonly ReconnectArgs reconnect = new ReconnectArgs ();
        private readonly object keepOneTransform = new object ();
        //private
        private CancellationTokenSource timeoutCts;
        private CancellationTokenSource delayCts;
        private NetResolverLite transform;
        private string realRoomId;
        private bool isEnabled;
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

        public DanmakuCarrier (IWebApi accessApi, IDanmakuResolver resolver, ILogger logger) {
            this.logger = logger;
            this.accessApi = accessApi;
            this.resolver = resolver;
            this.emitter = new EventSubmitHandler {
                OnException = e => logger.log(Level.Warning,
                $"Chat transform exception : {e?.Message}"),
                HotUpdated = resolver.onHotUpdateByDanmaku,
                InActive = onInactive,
                OnMessage = emitDanmaku,
                Active = onActive
            };
        }

        public void resetState () {
            IsLiveOn = default(bool?);
            reconnect.reset ();
            if (IsChannelActive) {
                disconnect ();
            }
        }

        public void syncStatusFrom(IRoomInfo info) {
            if(info != null) {
                updateIsLiveOnTo(info.IsOn);
            }
        }

        public void purgeEvents () => emitter.purgeEvents ();
        
        public void disconnect () {
            this.isEnabled = false;
            //close connection
            if (transform != null) {
                var temp = transform;
                transform = null;
                temp.close();
                temp.Resolvers.clear();
                temp = null;
            }
            if (timeoutCts?.Token.CanBeCanceled == true) {
                timeoutCts.Cancel();
            }
            if (delayCts?.Token.CanBeCanceled == true) {
                delayCts.Cancel();
            }
        }

        public void connectAsync (string realRoomId) {
            this.isEnabled = true;
            this.realRoomId = realRoomId;
            if (!IsChannelActive) {
                connectByApiAsync (accessApi, realRoomId);
            }
        }

        private bool connectByApiAsync (IWebApi biliApi, string realRoomId) {
            if (biliApi.tryGetValidDmServerBean (realRoomId.ToString (), out ServerData sd)) {
                logger.log (Level.Info, "Trying to connect to danmaku server.");
                activeTransformAsync (sd.Host, sd.Port, realRoomId);
                return true;
            } else {
                logger.log (Level.Error, "Cannot get valid server address and port.");
                return false;
            }
        }

        private bool activeTransformAsync (String host, int port, string realRoomId) {
            lock (keepOneTransform) {
                if (!isEnabled || IsChannelActive) return false;
                //............
                transform?.Resolvers.clear ();
                transform = new NetResolverLite ();
                transform.Resolvers.addLast (new KeepAliveHandler (realRoomId));
                transform.Resolvers.addLast (new UnpackHandler ());
                transform.Resolvers.addLast (emitter);
                transform.connectAsync (host, port);
            }
            resolver.onDanmakuConnecting();
            return true;
        }
        
        private void onActive () {
            logger.log (Level.Info, "Connect to danmaku server ok.");
            reconnect.reset ();
            resolver.onDanmakuActive();
        }

        private void onInactive(Exception e) {
            resolver.onDanmakuInactive();
            reconnectIfError(e);
        }

        private async void reconnectIfError(Exception e) {
            //TODO something here
            if (e != null) {
                logger.log(Level.Error, e.Message);
            }
            if (timeoutCts?.Token.CanBeCanceled == true) {
                timeoutCts.Cancel();
            }
            if (delayCts?.Token.CanBeCanceled == true) {
                delayCts.Cancel();
            }
            if (!isEnabled) { //donnot reconnect when download stopped.
                return;
            }
            if (!reconnect.canRetry()) {
                logger.log(Level.Error, "Retry time more than the max.");
                return;
            }
            //set cancellation and start task.
            bool connectionOK = false;
            long used = 3000;
            timeoutCts = new CancellationTokenSource(3000);
            Task.Run(() => {
                var sw = Stopwatch.StartNew();
                connectionOK = SharedHelper.checkCanConnect("live.bilibili.com");
                sw.Stop();
                used = sw.ElapsedMilliseconds;
            }, timeoutCts.Token).Wait();
            delayCts = new CancellationTokenSource();
            var delay = reconnect.DelayReconnectMs - used;
            if (delay > 0) {
                logger.log(Level.Info, $"Trying to reconnect to danmaku server after {(delay) / (double)1000}s");
                try {
                    await Task.Delay(TimeSpan.FromMilliseconds(delay), delayCts.Token);
                }catch{
                    logger.log(Level.Info, $"Delay exception occurred");
                    return;
                }
            } else logger.log(Level.Info, "Trying to reconnect to danmaku server.");
            if (!isEnabled)
                return;
            //increase delay
            reconnect.DelayReconnectMs += (connectionOK ? 1000 : reconnect.RetryTimes * 2000);
            reconnect.RetryTimes++;
            connectByApiAsync(accessApi, realRoomId);
        }

        private void emitDanmaku (DanmakuModel danmaku) {
            if (!isEnabled /*May not come here*/ ) return;
            findLiveCommandFrom (danmaku);
            PretreatDanmaku?.Invoke(danmaku);
            resolver.onDanmakuReceive(danmaku);
        }

        private void findLiveCommandFrom (DanmakuModel danmaku) {
            if (MsgTypeEnum.LiveEnd == danmaku.MsgType) {
                //Update live status
                updateIsLiveOnTo (false);
                logger.log (Level.Info, "Message received : Live End.");
                LiveCommandRecv?.Invoke(MsgTypeEnum.LiveEnd);
            } else if (MsgTypeEnum.LiveStart == danmaku.MsgType) {
                //Update live status
                updateIsLiveOnTo (true);
                logger.log (Level.Info, "Message received : Live Start.");
                LiveCommandRecv?.Invoke(MsgTypeEnum.LiveStart);
            }
        }

        private void updateIsLiveOnTo (bool isLiveOn, bool raiseEvent = true) {
            if (this.IsLiveOn != isLiveOn) {
                this.IsLiveOn = isLiveOn;
                if (raiseEvent) {
                    resolver.onLiveStatusUpdate(isLiveOn);
                }
            }
        }

    }
}