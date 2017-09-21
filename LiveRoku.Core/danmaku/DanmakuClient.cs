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

        public bool start (BiliApi api, int realRoomId) {
            lock (locker) {
                if (isActive()) return true;
                bool mayNotExist;
                string host;
                string portText;
                int port;
                if (!api.getDmServerAddr(realRoomId.ToString(), out host, out portText, out mayNotExist)) {
                    if (mayNotExist)
                        return false;
                    //May exist, generate default address
                    var hosts = BiliApi.Const.DefaultHosts;
                    host = hosts[new Random().Next(hosts.Length)];
                    port = BiliApi.Const.DefaultChatPort;
                } else if (!int.TryParse(portText, out port)) {
                    port = BiliApi.Const.DefaultChatPort;
                }
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