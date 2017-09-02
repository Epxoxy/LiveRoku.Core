namespace LiveRoku.Core {
    using System;
    public class DynamicBuffer {
        public byte[] Buffer { get; private set; }
        public int DataSize { get; private set; }
        private int readIndex = 0;
        public DynamicBuffer (int bufferSize) {
            DataSize = 0;
            Buffer = new byte[bufferSize];
        }
        public int getReserveSize () => Buffer.Length - DataSize;

        public void setBufferSize (int size) {
            if (Buffer.Length < size) {
                byte[] bufferTemp = new byte[size];
                //Copy old data
                Array.Copy (Buffer, 0, bufferTemp, 0, DataSize);
                Buffer = bufferTemp; //Replace buffer
            }
        }
        public void writeBuffer (byte[] buffer) => writeBuffer (buffer, 0, buffer.Length);

        /// <summary>
        /// Write bytes to buffer from bytes array's offset to it's count
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        public void writeBuffer (byte[] buffer, int offset, int count) {
            if (getReserveSize () >= count) {
                //Write data when space is enough
                Array.Copy (buffer, offset, Buffer, DataSize, count);
                DataSize = DataSize + count;
            } else {
                //Alloc more memory when reserver size is lower
                int totalSize = Buffer.Length + count - getReserveSize ();
                byte[] tmpBuffer = new byte[totalSize];
                //Make a copy of old data
                Array.Copy (Buffer, 0, tmpBuffer, 0, DataSize);
                //Write down new data
                Array.Copy (buffer, offset, tmpBuffer, DataSize, count);
                DataSize = DataSize + count;
                Buffer = tmpBuffer; //Assign to current
            }
        }

        private void fixSize (int count) {
            if (getReserveSize () >= count) {
                //
            }
        }

        //read from readIndex to len
        private byte[] read (int count) {
            var bytes = new byte[count];
            Array.Copy (Buffer, readIndex, bytes, 0, count);
            if (BitConverter.IsLittleEndian) {
                Array.Reverse (bytes);
            }
            readIndex += count;
            return bytes;
        }

        public ushort readUshort () => read (2).toUInt16 ();
        public short readShort () => read (2).toInt16 ();
        public uint readUint () => read (4).toUInt32 ();
        public int readInt () => read (4).toInt32 ();
        public ulong readUlong () => read (8).toUInt64 ();
        public long readLong () => read (8).toInt64 ();
        public float readFloat () => read (4).toFloat ();
        public double readDouble () => read (8).toDouble ();
        public byte readByte () => Buffer[readIndex++];

        public void readBytes (byte[] disbytes, int disstart, int len) {
            int size = disstart + len;
            for (int i = disstart; i < size; i++) {
                disbytes[i] = readByte ();
            }
        }
        public void setReaderIndex (int index) {
            if (index < 0) return;
            readIndex = index;
        }

        public void clear () => DataSize = 0;

        public void clear (int length) {
            if (length >= DataSize) {
                DataSize = 0;
            } else {
                //Move data forward
                for (int i = 0; i < DataSize - length; i++) {
                    Buffer[i] = Buffer[length + i];
                }
                DataSize -= length;
            }
        }
    }

}