using System;
using System.Diagnostics;

namespace LiveRoku.Core {
    internal class DanmakuClient {
        public EventSubmitHandler Events => eventHandler;
        private EventSubmitHandler eventHandler;
        private NetResolverLite transform;
        private object locker = new object();

        public DanmakuClient () {
            eventHandler = new EventSubmitHandler ();
        }

        public bool isActive () => transform == null ? false : transform.isActive ();

        public bool start (String host, int port, int realRoomId) {
            lock (locker) {
                if (isActive()) return true;
                //............
                transform = new NetResolverLite();
                transform.Resolvers.addLast(new KeepAliveHandler(realRoomId));
                transform.Resolvers.addLast(new UnpackHandler());
                transform.Resolvers.addLast(eventHandler);
                transform.connectAsync(host, port);
                return true;
            }
        }

        public void stop () {
            if (transform != null && transform.isActive ()) {
                transform.close ();
                transform = null;
            }
        }
        
    }
}