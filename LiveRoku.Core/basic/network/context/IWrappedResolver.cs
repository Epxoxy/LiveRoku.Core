namespace LiveRoku.Core {
    using System;
    using System.Threading.Tasks;

    public interface IWrappedResolver : ITransformContext {
        IFlowResolver Resolver { get; }
        IWrappedResolver Next { get; set; }
    }

    internal class WrappedFlowNodeContext : IWrappedResolver {
        public IFlowResolver Resolver { get; }
        public IWrappedResolver Next { get; set; }
        protected ITransform transform;

        public WrappedFlowNodeContext (ITransform transform, IFlowResolver resolver) {
            this.transform = transform;
            this.Resolver = resolver;
        }
        internal WrappedFlowNodeContext (ITransform transform, IFlowResolver resolver, IWrappedResolver next) : this (transform, resolver) {
            this.Next = next;
        }
        public WrappedFlowNodeContext (ITransform transform, IFlowResolver resolver, IFlowResolver next) : this (transform, resolver, new WrappedFlowNodeContext (transform, next)) { }

        public virtual void fireConnected () {
            var next = Next;
            next?.Resolver ? .onActive (next);
        }

        public virtual void fireRead (object data) {
            var next = Next;
            next?.Resolver ? .onRead (next, data);
        }

        public virtual void fireReadReady (object data) {
            var next = Next;
            next?.Resolver ? .onReadReady (next, data);
        }

        public virtual void fireClosed (object data) {
            var next = Next;
            next?.Resolver ? .onInactive (next, data);
        }

        public virtual void fireException (Exception e) {
            var next = Next;
            next?.Resolver ? .onException (next, e);
        }

        public bool isActive () => transform.isActive ();
        public Task connectAsync (string host, int port) => transform.connectAsync (host, port);
        public bool writeAndFlush (byte[] data) => transform.writeAndFlush (data);
        public bool write (byte[] data) => transform.write (data);
        public bool flush () => transform.flush ();
        public void close () => transform.close ();
    }

}