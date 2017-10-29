namespace LiveRoku.Core{
    internal class PacketEncoder{
        public ByteBuffer encode(Packet packet, ByteBuffer output){
            byte[] payload = System.Text.Encoding.UTF8.GetBytes(packet.payload);
            packet.length = payload.Length + Packet.HeaderSize;
            packet.headerLength = Packet.HeaderSize;
            try {
                output.writeInt(packet.length);
                output.writeShort(packet.headerLength);
                output.writeShort(packet.devType);
                output.writeInt(packet.packetType);
                output.writeInt(packet.device);
                if (payload.Length > 0)
                    output.writeBytes(payload);
            }catch(System.Exception e) {
                e.printStackTrace();
            }
            return output;
        }
    }
}
