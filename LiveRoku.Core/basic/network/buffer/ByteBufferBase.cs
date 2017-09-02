namespace LiveRoku.Core {
    using System;

    public abstract class ByteBufferBase {
        protected byte[] buf;
        protected int capacity;
        protected int readIndex = 0;
        protected int writeIndex = 0;
        protected int markReadIndex = 0;
        protected int markWriteIndex = 0;
        protected ByteBufferBase (int capacity) {
            buf = new byte[capacity];
            this.capacity = capacity;
        }
        protected ByteBufferBase (byte[] bytes) {
            buf = bytes;
            this.capacity = bytes.Length;
        }

        public int ReadableBytes => writeIndex - readIndex;
        public int GetCapacity => this.capacity;

        public void setReaderIndex (int index) {
            if (index < 0) return;
            readIndex = index;
        }
        //
        public void markReaderIndex () => markReadIndex = readIndex;
        public void markWriterIndex () => markWriteIndex = writeIndex;
        public void resetReaderIndex () => readIndex = markReadIndex;
        public void resetWriterIndex () => writeIndex = markWriteIndex;

        //write ReadableBytes to this from ByteBuffer
        public abstract void write (ByteBuffer buffer);
        public abstract void writeByte (byte value);
        public void writeBytes (byte[] bytes) => writeBytes (bytes, bytes.Length);
        public void writeBytes (byte[] bytes, int length) => writeBytes (bytes, 0, length);
        public abstract void writeBytes (byte[] bytes, int startIndex, int length);

        public void writeShort (short value) => writeBytes (BitConverter.GetBytes (value).flip ());
        public void writeUshort (ushort value) => writeBytes (BitConverter.GetBytes (value).flip ());
        //byte[] array = new byte[4];
        //for (int i = 3; i >= 0; i--)
        //{
        //    array[i] = (byte)(value & 0xff);
        //    value = value >> 8;
        //}
        //Array.Reverse(array);
        //Write(array);
        public void writeInt (int value) => writeBytes ((BitConverter.GetBytes (value)).flip ());
        public void writeUint (uint value) => writeBytes (BitConverter.GetBytes (value).flip ());
        public void writeLong (long value) => writeBytes (BitConverter.GetBytes (value).flip ());
        public void writeUlong (ulong value) => writeBytes (BitConverter.GetBytes (value).flip ());
        public void writeFloat (float value) => writeBytes (BitConverter.GetBytes (value).flip ());
        public void writeDouble (double value) => writeBytes (BitConverter.GetBytes (value).flip ());

        protected abstract byte[] read (int size);
        public abstract byte readByte ();
        public abstract void readBytes (byte[] disbytes, int disstart, int len);
        public ushort readUshort () => read (2).toUInt16 ();
        public short readShort () => read (2).toInt16 ();
        public uint readUint () => read (4).toUInt32 ();
        public int readInt () => read (4).toInt32 ();
        public ulong readUlong () => read (8).toUInt64 ();
        public long readLong () => read (8).toInt64 ();
        public float readFloat () => read (4).toFloat ();
        public double readDouble () => read (8).toDouble ();

        public abstract void discardReadBytes ();

        public abstract void clear ();

        public abstract byte[] toArray ();
    }

    public static class BitConverterHelper {
        public static byte[] flip (this byte[] bytes) {
            if (BitConverter.IsLittleEndian) {
                Array.Reverse (bytes);
            }
            return bytes;
        }
        public static short toInt16 (this byte[] data, int index = 0) {
            return BitConverter.ToInt16 (data, index);
        }
        public static ushort toUInt16 (this byte[] data, int index = 0) {
            return BitConverter.ToUInt16 (data, index);
        }
        public static int toInt32 (this byte[] data, int index = 0) {
            return BitConverter.ToInt32 (data, index);
        }
        public static uint toUInt32 (this byte[] data, int index = 0) {
            return BitConverter.ToUInt32 (data, index);
        }
        public static long toInt64 (this byte[] data, int index = 0) {
            return BitConverter.ToInt64 (data, index);
        }
        public static ulong toUInt64 (this byte[] data, int index = 0) {
            return BitConverter.ToUInt64 (data, index);
        }
        public static float toFloat (this byte[] data, int index = 0) {
            return BitConverter.ToSingle (data, index);
        }
        public static double toDouble (this byte[] data, int index = 0) {
            return BitConverter.ToDouble (data, index);
        }
    }

}