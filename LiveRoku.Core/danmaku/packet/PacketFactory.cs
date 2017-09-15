using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LiveRoku.Core {
    public class UnpackWatcher {
        public Action<ByteBuffer> Fired { get; set; }
        public Action<int> PreparingUnpack { get; set; }
        public Action UnpackLooping { get; set; }
        public Action<int, int> UnpackedHead { get; set; }
        public Action<DateTime, Packet> PacketReady { get; set; }
        public Action UnpackFail { get; set; }
    }

    internal class PacketFactory {
        private const int baseLength = 16;
        private const int maxLength = 10 * 1024 * 1024;
        private ByteBuffer workFlow;
        private static object lockHelper = new object ();
        public Action<Packet> PacketReadyDo { get; set; }

        public UnpackWatcher UnpackWatcher { get; set; } //For debugging
        public PacketFactory (ByteBuffer workFlow = null) {
            this.workFlow = workFlow;
        }

        public byte[] pack (Packet packet) {
            var payload = Encoding.UTF8.GetBytes (packet.payload);
            packet.length = payload.Length + baseLength;
            packet.headerLength = baseLength;
            try {
                var buf = ByteBuffer.allocate (packet.length);
                buf.writeInt (packet.length);
                buf.writeShort (packet.headerLength);
                buf.writeShort (packet.devType);
                buf.writeInt (packet.packetType);
                buf.writeInt (packet.device);
                if (payload.Length > 0)
                    buf.writeBytes (payload);
                return buf.toArray ();
            } catch (Exception e) {
                e.printStackTrace ();
                return null;
            }
        }

        public byte[] packSimple (int msgType, string payload) {
            return pack (new Packet () {
                devType = 1,
                    packetType = msgType,
                    device = 1,
                    payload = payload
            });
        }

        public void setWorkFlow (ByteBuffer workFlow) {
            this.workFlow = workFlow;
        }

        public void fireUnpack () {
            if (workFlow == null) return;
            UnpackWatcher?.Fired ? .Invoke (workFlow);
            if (workFlow.ReadableBytes >= baseLength && Monitor.TryEnter (lockHelper)) {
                Monitor.Exit (lockHelper);
                readyUnpack ();
            }
        }

        private void readyUnpack () {
            Task.Run (() => {
                lock (lockHelper) {
                    System.Diagnostics.Debug.WriteLine("****************unpack");
                    var packetAvailable = true;
                    UnpackWatcher?.PreparingUnpack ? .Invoke (workFlow.ReadableBytes);
                    while (packetAvailable) {
                        try {
                            UnpackWatcher?.UnpackLooping ? .Invoke ();
                            unpack (workFlow);
                            if (workFlow.ReadableBytes < baseLength) {
                                packetAvailable = false;
                                break;
                            }
                        } catch (Exception e) {
                            System.Diagnostics.Debug.WriteLine (e.ToString ());
                            e.printStackTrace ();
                            packetAvailable = false;
                            break;
                        }
                    }
                }
            }).ContinueWith (task => {
                task.Exception?.printStackTrace ();
            });
        }

        private bool unpack (ByteBuffer flow) {
            if (flow.ReadableBytes < baseLength)
                return false;
            flow.markReaderIndex ();
            int packetLength = flow.readInt ();
            int payloadLength = packetLength - sizeof (int);
            if (packetLength < baseLength ||
                flow.ReadableBytes < payloadLength) {
                flow.resetReaderIndex ();
                return false;
            }
            UnpackWatcher?.UnpackedHead ? .Invoke (packetLength, flow.ReadableBytes + sizeof (int));
            if (packetLength < maxLength) {
                Packet packet = default (Packet);
                if (unpackPayload (flow, packetLength, out packet)) {
                    flow.discardReadBytes (); //Same to buf.clear();
                    PacketReadyDo?.Invoke (packet);
                    UnpackWatcher?.PacketReady ? .Invoke (DateTime.Now, packet);
                } else UnpackWatcher?.UnpackFail ? .Invoke ();
                /*else packet receive not complete*/
                return true;
            } else {
                flow.discardReadBytes ();
                return true;
            }

        }

        private bool unpackPayload (ByteBuffer flow, int packetLength, out Packet packet) {
            packet = new Packet { length = packetLength };
            packet.headerLength = flow.readShort ();
            packet.devType = flow.readShort ();
            packet.packetType = flow.readInt ();
            packet.device = flow.readInt ();
            packet.payloadLength = packetLength - baseLength;
            byte[] payload = null;
            switch (packet.packetType) {
                case 1: //Hot update
                case 2: //Hot update
                case 3: //Hot update
                    var hot = flow.readInt ();
                    packet.payload = hot.ToString ();
                    break;
                case 5: //danmaku data
                    payload = new byte[packet.payloadLength];
                    flow.readBytes (payload, 0, payload.Length);
                    packet.payload = Encoding.UTF8.GetString (payload);
                    if (!isValid (packet.payload)) {
                        return false;
                    }
                    break;
                case 4: //unknow
                case 6: //newScrollMessage
                case 7:
                case 8: //hand shake ok.
                case 16:
                default:
                    payload = new byte[packet.payloadLength];
                    flow.readBytes (payload, 0, payload.Length);
                    packet.payload = Encoding.UTF8.GetString (payload);
                    break;
            }
            return true;
        }

        private bool isValid (string json) {
            try {
                //
                return true;
            } catch (Exception e) {
                e.printStackTrace ();
                return false;
            }
        }

    }
}