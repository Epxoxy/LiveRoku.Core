namespace LiveRoku.Core{
    using System;
    public class EventSubmitHandler : AbstractFlowResolver {
        public event DanmakuReceivedHandler DanmakuReceived;
        public event ConnectedHandler Connected;
        public event DisconnectedHandler Closed;
        public event HotUpdatedHandler HotUpdated;
        public event LogHandler ErrorLog;

        public override void onConnected(ITransformContext ctx){
            Connected?.Invoke();
        }
        
        public override void onRead(ITransformContext ctx, object data) {
            if(data != null && data is Packet) {
                var packet = (Packet)data;
                checkPacket(packet);
            }
        }
        
        private void checkPacket(Packet packet){
            switch (packet.packetType){
                case 1: //online count param
                case 2: //online count param
                case 3: //online count param
                    var num = int.Parse(packet.payload);
                    HotUpdated?.Invoke(num);
                    break;
                case 5: //danmaku data
                    var nowTime = Convert.ToInt64((DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0)).TotalMilliseconds);
                    var danmaku = DanmakuFactory.parse(packet.payload, nowTime, 2);
                    DanmakuReceived?.Invoke(danmaku);
                    break;
                case 4: //unknow
                case 6: //newScrollMessage
                case 7:
                case 16:
                default: break;
            }
        }

        public override void onClosed(ITransformContext ctx, object data) {
            var error = data == null ? null : data as Exception;
            Closed?.Invoke(error);
        }

        public override void onException(ITransformContext ctx, Exception e) {
            System.Diagnostics.Debug.WriteLine(e.ToString());
            ErrorLog?.Invoke(e.Message);
        }

        private bool tryGet<T>(object obj, out T value) where T : class {
            value = default(T);
            if (obj == null) return false;
            value = obj as T;
            return value != null;
        }
    }
}
