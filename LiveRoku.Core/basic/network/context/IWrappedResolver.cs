using System;
using System.Threading.Tasks;

namespace LiveRoku.Core {

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
            Next?.Resolver ? .onConnected (Next);
        }

        public virtual void fireRead (object data) {
            Next?.Resolver ? .onRead (Next, data);
        }

        public virtual void fireReadReady (object data) {
            Next?.Resolver ? .onReadReady (Next, data);
        }

        public virtual void fireClosed (object data) {
            Next?.Resolver ? .onClosed (Next, data);
        }

        public virtual void fireException (Exception e) {
            Next?.Resolver ? .onException (Next, e);
        }

        public bool isActive () => transform.isActive ();
        public Task connectAsync (string host, int port) => transform.connectAsync (host, port);
        public bool writeAndFlush (byte[] data) => transform.writeAndFlush (data);
        public bool write (byte[] data) => transform.write (data);
        public bool flush () => transform.flush ();
        public void close () => transform.close ();
    }

}