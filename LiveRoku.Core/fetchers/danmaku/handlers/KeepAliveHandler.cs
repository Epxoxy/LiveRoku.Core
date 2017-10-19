using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace LiveRoku.Core {
    public class KeepAliveHandler : AbstractFlowResolver {
        private CancellationTokenSource heartbeatCts;
        private int channelId;
        private int retryTimes = 3;

        public KeepAliveHandler (int channelId) {
            this.channelId = channelId;
        }

        [SuppressMessage ("Microsoft.Performance", "CS4014")]
        public override void onConnected (ITransformContext ctx) {
            //Handshake
            System.Diagnostics.Debug.WriteLine ("Invoke KeepAliveHandler.onConnected(ctx)", "INFO");
            var tmpUid = (long) (1e14 + 2e14 * new Random ().NextDouble ());
            var payload = "{ \"roomid\":" + channelId + ", \"uid\":" + tmpUid + "}";
            var handshake = Packet.packSimple (PacketMsgType.Handshake, payload);
            var handshakeBytes = new PacketEncoder().encode(handshake, ByteBuffer.allocate(16)).toArray();
            try {
                ctx.writeAndFlush (handshakeBytes);
            } catch (Exception e) {
                e.printStackTrace ();
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
                        System.Diagnostics.Debug.WriteLine ("heartbeat...", "INFO");
                    } catch (Exception e) {
                        e.printStackTrace ();
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
            base.onConnected (ctx);
        }

        public override void onClosed(ITransformContext ctx, object data) {
            cancelHeartbeat();
            base.onClosed(ctx, data);
        }

        private void cancelHeartbeat() {
            if(heartbeatCts!=null && heartbeatCts.Token.CanBeCanceled) {
                heartbeatCts.Cancel();
            }
        }
    }
}