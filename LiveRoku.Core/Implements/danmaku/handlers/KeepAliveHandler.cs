namespace LiveRoku.Core.Danmaku.Handlers {
    using LiveRoku.Core.Common;
    using LiveRoku.Core.Danmaku.Codec;
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading;
    using System.Threading.Tasks;
    public class KeepAliveHandler : AbstractFlowResolver {
        private CancellationTokenSource heartbeatCts;
        private string channelId;
        private int retryTimes = 3;

        public KeepAliveHandler (string channelId) {
            this.channelId = channelId;
        }

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);
            heartbeatCts?.Dispose();
        }

        [SuppressMessage ("Microsoft.Performance", "CS4014")]
        public override void onActive (ITransformContext ctx) {
            //Handshake
            System.Diagnostics.Debug.WriteLine ("Invoke KeepAliveHandler.onConnected(ctx)", "heartbeat");
            var tmpUid = (long) (1e14 + 2e14 * new Random ().NextDouble ());
            var payload = "{ \"roomid\":" + channelId + ", \"uid\":" + tmpUid + "}";
            var handshake = Packet.packSimple (PacketMsgType.Handshake, payload);
            var handshakeBytes = new PacketEncoder().encode(handshake, ByteBuffer.allocate(16)).toArray();
            try {
                ctx.writeAndFlush (handshakeBytes);
            } catch (Exception e) {
                e.printStackTrace();
                ctx.close ();
                return;
            }
            //Heartbeat
            cancelHeartbeat();
            heartbeatCts = new CancellationTokenSource();
            Task.Run (async () => {
                var errorTimes = 0;
                var ping = Packet.packSimple(PacketMsgType.Heartbeat, payload: string.Empty);
                var pingBytes = new PacketEncoder().encode(ping, ByteBuffer.allocate(16)).toArray();
                while (ctx.isActive ()) {
                    try {
                        ctx.writeAndFlush (pingBytes);
                        System.Diagnostics.Debug.WriteLine ("heartbeat sent...", "heartbeat");
                    } catch (Exception e) {
                        e.printStackTrace();
                        if (errorTimes > retryTimes) break;
                        ++errorTimes;
                        continue;
                    }
                    await Task.Delay (30000);
                }
                ctx.close ();
            }, heartbeatCts.Token).ContinueWith (task => {
                task.Exception?.printStackTrace ();
            }, TaskContinuationOptions.OnlyOnFaulted);
            base.onActive (ctx);
        }

        public override void onInactive(ITransformContext ctx, object data) {
            cancelHeartbeat();
            base.onInactive(ctx, data);
        }

        private void cancelHeartbeat() {
            if(heartbeatCts?.Token.CanBeCanceled == true) {
                heartbeatCts.Cancel();
            }
        }
    }
}