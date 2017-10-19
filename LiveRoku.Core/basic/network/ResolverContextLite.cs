using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace LiveRoku.Core {

    public class NetResolverLite : ITransform {
        private bool isAlive = false;
        private TcpClient client;
        private NetworkStream stream;
        public INodeFlow Resolvers => ctx;
        private HeadNodeContextLite ctx;
        private object locker = new object ();

        public NetResolverLite () {
            ctx = new HeadNodeContextLite (this);
        }

        public Task connectAsync (string host, int port) {
            lock (locker) {
                if (isAlive) return Task.FromResult (false);
                isAlive = true;
            }
            client = new TcpClient ();
            try {
                client.Connect (host, port);
                stream = client.GetStream ();
            } catch (Exception e) {
                try {
                    stream.Close ();
                    client.Close ();
                    stream = null;
                    client = null;
                } catch (Exception e2) {
                    e2.printStackTrace ();
                }
                ctx.fireException (e);
                return Task.FromResult (false);
            }
            ctx.fireConnected ();
            return Task.Run (async () => {
                while (isAlive && isOnline (client)) {
                    if (!stream.DataAvailable) {
                        await Task.Delay (100);
                        continue;
                    }
                    int readSize = 0;
                    var cache = new byte[1024];
                    var buffer = ByteBuffer.allocate (65535);
                    ctx.fireReadReady (buffer);
                    try {
                        while ((readSize = stream.Read (cache, 0, cache.Length)) > 0) {
                            buffer.writeBytes (cache, 0, readSize);
                            System.Diagnostics.Debug.WriteLine ("## read ## " + readSize);
                            ctx.fireRead (buffer);
                        }
                    } catch (Exception e) {
                        e.printStackTrace ();
                        ctx.fireException (e);
                    }
                }
                close ();
            }).ContinueWith (task => {
                task.Exception?.printStackTrace ();
            }, TaskContinuationOptions.OnlyOnFaulted);
        }

        private bool isOnline (TcpClient client) {
            return !((client.Client.Poll (1000, SelectMode.SelectRead) && (client.Client.Available == 0)) || !client.Client.Connected);
        }

        public bool isActive () => isAlive;

        public bool write (byte[] bytes) {
            try {
                stream.Write (bytes, 0, bytes.Length);
                return true;
            } catch (Exception e) {
                e.printStackTrace ();
                ctx.fireException (e);
                return false;
            }
        }

        public bool flush () {
            try {
                stream.Flush ();
                return true;
            } catch (Exception e) {
                e.printStackTrace ();
                ctx.fireException (e);
                return false;
            }
        }

        public bool writeAndFlush (byte[] data) {
            if (!isAlive) return false;
            return write (data) && flush ();
        }

        public void close () {
            lock (locker) {
                if (!isAlive) return;
                isAlive = false;
            }
            stream = null;
            try {
                var temp = client;
                client = null;
                temp.Close ();
                ctx.fireClosed (null);
            } catch (Exception e) {
                e.printStackTrace ();
                ctx.fireException (e);
            }
        }

    }

    public interface INodeFlow {
        void addLast (IFlowResolver resolver);
        void remove (IFlowResolver resolver);
        void clear ();
    }

    internal class HeadNodeContextLite : WrappedFlowNodeContext, INodeFlow {
        private IWrappedResolver tail = null;

        public HeadNodeContextLite (ITransform transform) : base (transform, null) { }

        public void addLast (IFlowResolver resolver) {
            var ctx = new WrappedFlowNodeContext (transform, resolver);
            if (tail == null) {
                tail = this;
            }
            tail.Next = ctx;
            tail = ctx;
        }

        public void remove (IFlowResolver resolver) {
            IWrappedResolver current = this;
            IWrappedResolver previous = this;
            while (current != null) {
                if (current.Resolver == resolver) {
                    previous.Next = current.Next;
                    if (current == tail) {
                        tail = previous;
                    }
                }
                //Prepare for next check
                previous = current;
                current = current.Next;
            }
        }

        public void clear () {
            this.Next = null;
        }

        public override void fireConnected () => Task.Run (() => base.fireConnected ());
        public override void fireRead (object data) => Task.Run (() => base.fireRead (data));
        public override void fireReadReady (object data) => Task.Run (() => base.fireReadReady (data));
        public override void fireClosed (object data) => Task.Run (() => base.fireClosed (data));
        public override void fireException (Exception e) => Task.Run (() => base.fireException (e));

    }

}