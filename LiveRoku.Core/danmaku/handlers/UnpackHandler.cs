namespace LiveRoku.Core {
    public class UnpackHandler : AbstractFlowResolver {
        private PacketFactory factory = new PacketFactory ();
        private ITransformContext ctxTemp;

        public UnpackHandler () { }
        public UnpackHandler (UnpackWatcher unpackWatcher) {
            factory.UnpackWatcher = unpackWatcher;
            factory.PacketReadyDo = p => {
                ctxTemp?.fireRead (p);
            };
        }

        public override void onReadReady (ITransformContext ctx, object data) {
            this.ctxTemp = ctx;
            factory.setWorkFlow ((ByteBuffer) data);
            factory.fireUnpack ();
            base.onReadReady (ctx, data);
        }

        public override void onRead (ITransformContext ctx, object data) {
            factory.fireUnpack ();
        }
    }
}