namespace LiveRoku.Core {
    public class PacketMsgType {
        public const int Heartbeat = 2;
        public const int Handshake = 7;
    }
    public struct Packet {
        public static short HeaderSize = 16;
        public int length;
        public short headerLength;
        public short devType;
        public int packetType;
        public int device;
        public string payload;
        public int payloadLength;

        
        public static Packet packSimple (int msgType, string payload) {
            return new Packet{
                devType = 1,
                    packetType = msgType,
                    device = 1,
                    payload = payload
            };
        }

        public override string ToString () {
            return $"length[{length}],header[{headerLength}],devType:{devType},device:{device},msgType:{packetType}\n\tpayload[{payloadLength}]:{payload}";
        }
    }

}