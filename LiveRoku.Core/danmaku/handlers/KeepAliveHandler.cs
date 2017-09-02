using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace LiveRoku.Core {
    public class KeepAliveHandler : AbstractFlowResolver {
        private PacketFactory factory = new PacketFactory ();
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
            var bytes = factory.packSimple (PacketMsgType.Handshake, payload);
            try {
                ctx.writeAndFlush (bytes);
            } catch (Exception e) {
                e.printStackTrace ();
                ctx.close ();
                return;
            }
            //Heartbeat
            Task.Run (async () => {
                var errorTimes = 0;
                var ping = factory.packSimple (PacketMsgType.Heartbeat, payload : string.Empty);
                while (ctx.isActive ()) {
                    try {
                        ctx.writeAndFlush (ping);
                        System.Diagnostics.Debug.WriteLine ("Heartbeat...", "INFO");
                    } catch (Exception e) {
                        e.printStackTrace ();
                        if (errorTimes > retryTimes) break;
                        ++errorTimes;
                        continue;
                    }
                    await Task.Delay (30000);
                }
                ctx.close ();
            }).ContinueWith (task => {
                task.Exception?.printStackTrace ();
            });
            base.onConnected (ctx);
        }

    }
}