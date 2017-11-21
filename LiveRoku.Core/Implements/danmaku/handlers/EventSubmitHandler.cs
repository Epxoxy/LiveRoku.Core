namespace LiveRoku.Core.Danmaku {
    using LiveRoku.Core.Common;
    using LiveRoku.Core.Danmaku.Codec;
    using System;
    public class EventSubmitHandler : AbstractFlowResolver {
        public Action Active;
        public Action<Exception> InActive;
        public Action<long> HotUpdated;
        public Action<Base.DanmakuModel> OnMessage;
        public Action<Exception> OnException;
        
        public void purgeEvents () {
            OnMessage = delegate { };
            Active = delegate { };
            InActive = delegate { };
            HotUpdated = delegate { };
            OnException = delegate { };
        }

        public override void onActive (ITransformContext ctx) {
            Active?.Invoke ();
            //TODO Remove test code
            /*System.Threading.Tasks.Task.Run(async () => {
                await System.Threading.Tasks.Task.Delay(25000);
                int times = 50;
                var random = new Random();
                while (times--> 0) {
                    OnMessage?.Invoke(new Base.DanmakuModel { MsgType = Base.MsgTypeEnum.LiveStart });
                    await System.Threading.Tasks.Task.Delay(random.Next(20, 300));
                    OnMessage?.Invoke(new Base.DanmakuModel { MsgType = Base.MsgTypeEnum.LiveStart });
                    await System.Threading.Tasks.Task.Delay(random.Next(10, 200));
                }
                await System.Threading.Tasks.Task.Delay(20000);
                OnMessage?.Invoke(new Base.DanmakuModel { MsgType = Base.MsgTypeEnum.LiveEnd });
            });*/
        }

        public override void onRead (ITransformContext ctx, object data) {
            if (data != null && data is Packet) {
                checkPacket ((Packet) data);
            }
        }
        
        private void checkPacket (Packet packet) {
            switch (packet.packetType) {
                case 1: //online count param
                case 2: //online count param
                case 3: //online count param
                    var num = int.Parse (packet.payload);
                    HotUpdated?.Invoke (num);
                    break;
                case 5: //danmaku data
                    var nowTime = Convert.ToInt64 ((DateTime.UtcNow - new DateTime (1970, 1, 1, 0, 0, 0, 0)).TotalMilliseconds);
                    var danmaku = DanmakuFactory.parse (packet.payload, nowTime, 2);
                    OnMessage?.Invoke (danmaku);
                    break;
                case 4: //unknow
                case 6: //newScrollMessage
                case 7:
                case 16:
                default:
                    break;
            }
        }

        public override void onInactive (ITransformContext ctx, object data) {
            var error = data == null ? null : data as Exception;
            InActive?.Invoke (error);
        }

        public override void onException (ITransformContext ctx, Exception e) {
            System.Diagnostics.Debug.WriteLine (e.ToString ());
            OnException?.Invoke (e);
        }
    }
}