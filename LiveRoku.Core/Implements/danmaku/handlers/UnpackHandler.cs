namespace LiveRoku.Core {
    using LiveRoku.Core.Common;
    using System.Threading.Tasks;
    public class UnpackHandler : AbstractFlowResolver {
        private PacketDecoder decoder = new PacketDecoder();
        private readonly ByteBuffer cumulation = ByteBuffer.allocate(16);
        private static object locker = new object();

        public UnpackHandler () { }

        private void readyDecode(ITransformContext ctx, ByteBuffer buf) {
            lock (locker) {
                if (buf == null || buf.ReadableBytes <= 0) return;
                System.Diagnostics.Debug.WriteLine("--- enter ---", "decode");

                var readable = buf.ReadableBytes;
                System.Diagnostics.Debug.WriteLine("--- cumulation " + readable, "decode");

                var bytes = buf.copyDiscardBytes(readable);
                buf.discardReadBytes();

                cumulation.discardReadBytes();
                cumulation.writeBytes(bytes);
                while (cumulation.ReadableBytes > 0) {
                    var oldReaderIndex = cumulation.readerIndex();
                    var packet = decoder.decode(cumulation);
                    if (packet != null) {
                        cumulation.discardReadBytes();
                        System.Diagnostics.Debug.WriteLine($"--- {packet}", "decode");
                        Task.Run(() => ctx.fireRead(packet));
                    } else if (cumulation.readerIndex() == oldReaderIndex) {
                        System.Diagnostics.Debug.WriteLine("--- nothing read", "decode");
                        break;
                    }
                }
                System.Diagnostics.Debug.WriteLine("--- exit ---", "decode");
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