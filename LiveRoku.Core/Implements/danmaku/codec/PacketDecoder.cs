namespace LiveRoku.Core.Danmaku.Codec {
    using LiveRoku.Core.Common;
    internal class PacketDecoder {
        public object decode(ByteBuffer input) {
            if (input == null || input.ReadableBytes < Packet.HeaderSize) {
                System.Diagnostics.Debug.WriteLine("--- in.readableBytes() <= HeaderSize", nameof(decode));
                return null;
            }
            input.markReaderIndex();

            int packetLength = input.readInt();
            int payloadLength = packetLength - 4;
            if (packetLength < Packet.HeaderSize || input.ReadableBytes < payloadLength) {
                System.Diagnostics.Debug.WriteLine($"--- reset reader index, {packetLength}[need]/{input.ReadableBytes}[readable]", nameof(decode));
                input.resetReaderIndex();
                return null;
            }
            Packet packet = new Packet();
            packet.length = packetLength;
            packet.headerLength = input.readShort();
            packet.devType = input.readShort();
            packet.packetType = input.readInt();
            packet.device = input.readInt();
            packet.payloadLength = packetLength - Packet.HeaderSize;
            byte[] payload = null;
            switch (packet.packetType) {
                case 1: // Hot update
                case 2: // Hot update
                case 3: { // Hot update
                        int hot = input.readInt();
                        packet.payload = hot.ToString();
                    } break;
                case 5: {// danmaku data
                        payload = new byte[packet.payloadLength];
                        input.readBytes(payload, 0, payload.Length);
                        packet.payload = System.Text.Encoding.UTF8.GetString(payload);
                    } break;
                case 4: // unknow
                case 6: // newScrollMessage
                case 7:
                case 8: // hand shake ok.
                case 16:
                default:  {
                        payload = new byte[packet.payloadLength];
                        input.readBytes(payload, 0, payload.Length);
                        packet.payload = System.Text.Encoding.UTF8.GetString(payload);
                    } break;
            }
            return packet;
        }
    }
}
