using System;
using System.Diagnostics;

namespace LiveRoku.Core {
    internal class DanmakuClient {
        public EventSubmitHandler Events => eventHandler;
        private EventSubmitHandler eventHandler;
        private NetResolverLite transform;

        public DanmakuClient () {
            eventHandler = new EventSubmitHandler ();
        }

        public bool isActive () => transform == null ? false : transform.isActive ();

        public bool start (BiliApi api, int realRoomId) {
            stop ();
            bool mayNotExist;
            string host;
            string portText;
            int port;
            if (!api.getDmServerAddr (realRoomId.ToString (), out host, out portText, out mayNotExist)) {
                if (mayNotExist)
                    return false;
                //May exist, generate default address
                var hosts = BiliApi.Const.DefaultHosts;
                host = hosts[new Random ().Next (hosts.Length)];
                port = BiliApi.Const.DefaultChatPort;
            } else if (!int.TryParse (portText, out port)) {
                port = BiliApi.Const.DefaultChatPort;
            }
            //............
            transform = new NetResolverLite ();
            transform.Resolvers.addLast (new KeepAliveHandler (realRoomId));
            transform.Resolvers.addLast (new UnpackHandler (debugWatcher ()));
            transform.Resolvers.addLast (eventHandler);
            transform.connectAsync (host, port);
            return true;
        }

        public void stop () {
            if (transform != null && transform.isActive ()) {
                transform.close ();
                transform = null;
            }
        }

        private UnpackWatcher debugWatcher () {
            int maxAvailable = 0;
            return new UnpackWatcher {
                Fired = buf => {
                        Console.WriteLine ($"\n---------------------");
                        Console.WriteLine ($"Fire unpack {buf.ReadableBytes}");
                        if (buf.ReadableBytes > maxAvailable) {
                            maxAvailable = buf.ReadableBytes;
                            Debug.WriteLine ($"WorkFlow Max {maxAvailable}");
                        }
                    },
                    PreparingUnpack = size => {
                        Console.WriteLine ($"Continue unpack {size}");
                    },
                    UnpackedHead = (packetLength, total) => {
                        Console.WriteLine ("*******************************");
                        Console.WriteLine ($"{packetLength}/{total} Unpacking .......");
                        Console.WriteLine ("*******************************");
                    },
                    PacketReady = (d, p) => {
                        var now = d.ToString ("HH:mm:ss fff");
                        Console.WriteLine (now);
                        Debug.WriteLine (now);
                        Debug.WriteLine ("  " + p.ToString ());
                        Console.WriteLine ($"Unpacking, packetType-> {p.packetType}");
                        Console.WriteLine ("*******************************");
                    },
                    UnpackFail = () => {
                        Console.WriteLine ("\tUnpack fail.");
                        Console.WriteLine ("\tUnpack fail.");
                        Console.WriteLine ("*******************************");
                    },
                    UnpackLooping = () => {
                        Console.WriteLine ($"\n---------------------");
                        Console.WriteLine ($"[{DateTime.Now.ToString("HH:mm:ss fff")}] Unpacking loop");
                    }
            };
        }

    }
}