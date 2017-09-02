namespace LiveRoku.Core {
    public class UnpackHandler : AbstractFlowResolver {
        private PacketFactory factory = new PacketFactory ();
        private ITransformContext ctx;

        public UnpackHandler () { }
        public UnpackHandler (UnpackWatcher unpackWatcher) {
            factory.UnpackWatcher = unpackWatcher;
            factory.PacketReadyDo = p => {
                ctx?.fireRead (p);
            };
        }

        public override void onReadReady (ITransformContext ctx, object data) {
            factory.setWorkFlow ((ByteBuffer) data);
            factory.fireUnpack ();
            base.onReadReady (ctx, data);
        }

        public override void onRead (ITransformContext ctx, object data) {
            factory.fireUnpack ();
        }
    }
}