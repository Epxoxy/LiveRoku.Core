namespace LiveRoku.Core.Danmaku {
    using LiveRoku.Base;
    using LiveRoku.Base.Logger;
    using LiveRoku.Core.Common;
    using LiveRoku.Core.Models;
    using System;

    internal class DanmakuCarrier {
        public bool IsChannelActive => transform?.isActive() == true;
        public bool? IsLiveOn { get; private set; }
        public Action<MsgTypeEnum> LiveCommandRecv { get; set; }
        public Action<DanmakuModel> PretreatDanmaku { get; set; }
        public Action InactiveTotally { get; set; }
        //private readonly
        private readonly ILogger logger;
        private readonly IWebApi accessApi; //API access
        private readonly EventSubmitHandler emitter;
        private readonly IDanmakuResolver resolver;
        private readonly object keepOneTransform = new object ();
        //private
        private NetResolverLite transform;
        private ReconnectHandler reconnector;
        private string realRoomId;
        private bool isEnabled;
        
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
            reconnector?.doNotReconnect();
            if (transform != null) {
                var temp = transform;
                transform = null;
                temp.close();
                temp.Dispose();
                temp = null;
            }
        }

        public void connectAsync (string realRoomId) {
            this.isEnabled = true;
            this.realRoomId = realRoomId;
            if (!IsChannelActive) {
                if (accessApi.tryGetValidDmServerBean(realRoomId.ToString(), out ServerData sd)) {
                    connectByServerBeanAsync(sd, realRoomId);
                } else {
                    logger.log(Level.Error, "Cannot get valid server address and port.");
                }
            }
        }
        
        private bool connectByServerBeanAsync(ServerData sd, string realRoomId) {
            logger.log(Level.Info, "Trying to connect to danmaku server.");
            lock (keepOneTransform) {
                if (!isEnabled || IsChannelActive)
                    return false;
                //............
                reconnector = null;
                transform?.Dispose();
                transform = new NetResolverLite();
                transform.Resolvers.addLast(new KeepAliveHandler(realRoomId));
                transform.Resolvers.addLast(new UnpackHandler());
                transform.Resolvers.addLast(emitter);
                transform.Resolvers.addLast((reconnector = new ReconnectHandler {
                    //FlowResolver fire in other thread
                    HowToReconnect = () => connectByServerBeanAsync(sd, realRoomId),
                    InactiveTotally = this.InactiveTotally
                }));
                transform.connectAsync(sd.Host, sd.Port);
            }
            resolver.onDanmakuConnecting();
            return true;
        }

        private void onActive () {
            logger.log (Level.Info, "Connect to danmaku server ok.");
            resolver.onDanmakuActive();
        }

        private void onInactive(Exception e) {
            if (e != null) {
                logger.log(Level.Error, e.Message);
            }
            resolver.onDanmakuInactive();
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