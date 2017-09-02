namespace LiveRoku.Core {
    public class PacketMsgType {
        public const int Heartbeat = 2;
        public const int Handshake = 7;
    }
    public struct Packet {
        public int length;
        public short headerLength;
        public short devType;
        public int packetType;
        public int device;
        public string payload;
        public int payloadLength;

        public override string ToString () {
            return $"length[{length}],header[{headerLength}],devType:{devType},device:{device},msgType:{packetType}\n\tpayload[{payloadLength}]:{payload}";
        }
    }

}