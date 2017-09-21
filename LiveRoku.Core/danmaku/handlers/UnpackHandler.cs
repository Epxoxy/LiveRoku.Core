using System.Threading.Tasks;

namespace LiveRoku.Core {
    public class UnpackHandler : AbstractFlowResolver {
        private PacketDecoder decoder = new PacketDecoder();
        private readonly ByteBuffer cumulation = ByteBuffer.allocate(16);
        private static object locker = new object();

        public UnpackHandler () { }

        private void readyDecode(ITransformContext ctx, ByteBuffer buf) {
            lock (locker) {
                if (buf == null || buf.ReadableBytes <= 0) return;
                System.Diagnostics.Debug.WriteLine("## enter ##");

                var readable = buf.ReadableBytes;
                System.Diagnostics.Debug.WriteLine("cumulation " + readable);

                var bytes = buf.copyDiscardBytes(readable);
                buf.discardReadBytes();

                cumulation.discardReadBytes();
                cumulation.writeBytes(bytes);
                while (cumulation.ReadableBytes > 0) {
                    var oldReaderIndex = cumulation.readerIndex();
                    var packet = decoder.decode(cumulation);
                    if (packet != null) {
                        cumulation.discardReadBytes();
                        System.Diagnostics.Debug.WriteLine(packet);
                        Task.Run(() => ctx.fireRead(packet));
                    } else if (cumulation.readerIndex() == oldReaderIndex) {
                        System.Diagnostics.Debug.WriteLine("nothing read");
                        break;
                    }
                }
                System.Diagnostics.Debug.WriteLine("## exit ##");
            }
        }
        
        public override void onReadReady (ITransformContext ctx, object data) {
            readyDecode(ctx, (ByteBuffer)data);
            base.onReadReady (ctx, data);
        }

        public override void onRead (ITransformContext ctx, object data) {
            readyDecode(ctx, (ByteBuffer)data);
        }
    }
}