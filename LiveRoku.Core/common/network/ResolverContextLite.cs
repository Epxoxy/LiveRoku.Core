﻿namespace LiveRoku.Core.Common {
    using System;
    using System.Diagnostics;
    using System.Net.Sockets;
    using System.Threading.Tasks;
    public class NetResolverLite : ITransform, IDisposable {
        private bool isAlive = false;
        private TcpClient client;
        private NetworkStream stream;
        public INodeFlow Resolvers => ctx;
        private HeadNodeContextLite ctx;
        private object locker = new object ();
        private int errorTimes = 0;

        public NetResolverLite () {
            ctx = new HeadNodeContextLite (this);
        }

        ~NetResolverLite() {
            Dispose(false);
        }

        public void Dispose() {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing) {
            client?.Close();
            ctx?.clear();
        }

        public Task connectAsync (string host, int port) {
            lock (locker) {
                if (isAlive) return Task.FromResult (false);
                isAlive = true;
            }
            client = new TcpClient();
            try {
                client.Connect (host, port);
                stream = client.GetStream ();
            } catch (Exception e) {
                ctx.fireException (e);
                close();
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
                            Debug.WriteLine ("--- read --- " + readSize, "network");
                            errorTimes = 0;//reset
                            ctx.fireRead (buffer);
                        }
                    } catch(System.IO.IOException e) when(e.InnerException != null) {
                        e.printStackTrace();
                        ctx.fireException(e);
                        SocketException se = null;
                        if ((se = e.InnerException as SocketException) != null) {
                            //10035 == WSAEWOULDBLOCK
                            if (se.NativeErrorCode.Equals(10035)) {
                                Debug.WriteLine("--- still connected.[10035] ---", "network");
                            } else {
                                break;
                            }
                        }
                    } catch (Exception e) {
                        e.printStackTrace();
                        ctx.fireException (e);
                        if(errorTimes++ > 10) {
                            Debug.WriteLine("--- errors over 10 ---", "network");
                            break;
                        } else if(e is ObjectDisposedException) {
                            break;
                        }
                    }
                }
                close ();
            }).ContinueWith (task => {
                task.Exception?.printStackTrace ();
            }, TaskContinuationOptions.OnlyOnFaulted);
        }

        private bool isOnline (TcpClient tcp) {
            return !((tcp.Client.Poll (1000, SelectMode.SelectRead) && (tcp.Client.Available == 0)) || !tcp.Client.Connected);
        }

        public bool isActive () => isAlive;

        public bool write (byte[] bytes) {
            try {
                stream.Write (bytes, 0, bytes.Length);
                return true;
            } catch (Exception e) {
                e.printStackTrace();
                ctx.fireException (e);
                return false;
            }
        }

        public bool flush () {
            try {
                stream.Flush ();
                return true;
            } catch (Exception e) {
                e.printStackTrace();
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
            try {
                client?.Close();
                stream?.Close();
                client = null;
                stream = null;
                ctx.fireClosed (null);
            } catch (Exception e) {
                e.printStackTrace();
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
            IWrappedResolver current = this.Next;
            IWrappedResolver next = this.Next?.Next;
            this.Next = null;
            while (current != null && next != null) {
                current.Resolver?.Dispose();
                current = next;
                next = current.Next;
            }
        }

        public override void fireConnected () => Task.Run (() => base.fireConnected ());
        public override void fireRead (object data) => Task.Run (() => base.fireRead (data));
        public override void fireReadReady (object data) => Task.Run (() => base.fireReadReady (data));
        public override void fireClosed (object data) => Task.Run (() => base.fireClosed (data));
        public override void fireException (Exception e) => Task.Run (() => base.fireException (e));
        
    }

}