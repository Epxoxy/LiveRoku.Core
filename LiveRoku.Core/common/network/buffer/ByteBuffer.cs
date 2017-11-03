namespace LiveRoku.Core.Common {
    using System;
    public class ByteBuffer : ByteBufferBase {
        private object locker = new object ();
        private ByteBuffer (int capacity) : base (capacity) { }
        private ByteBuffer (byte[] bytes) : base (bytes) { }

        public static ByteBuffer allocate (int capacity) {
            return new ByteBuffer (capacity);
        }

        public static ByteBuffer allocate (byte[] bytes) {
            return new ByteBuffer (bytes);
        }

        //write ReadableBytes to this from ByteBuffer
        public override void write (ByteBuffer buffer) {
            if (buffer == null) return;
            if (buffer.ReadableBytes <= 0) return;
            writeBytes (buffer.toArray ());
        }

        public override void writeByte (byte value) {
            lock (locker) {
                int afterLen = writeIndex + 1;
                fixSizeAndReset (buf.Length, afterLen);
                buf[writeIndex] = value;
                writeIndex = afterLen;
            }
        }
        public override void writeBytes (byte[] bytes, int startIndex, int length) {
            lock (locker) {
                int offset = length - startIndex;
                if (offset <= 0) return;
                int total = offset + writeIndex;
                fixSizeAndReset (buf.Length, total);
                for (int i = writeIndex, j = startIndex; i < total; i++, j++) {
                    buf[i] = bytes[j];
                }
                writeIndex = total;
            }
        }

        //read from readIndex to size
        protected override byte[] read (int size) {
            byte[] bytes = new byte[size];
            Array.Copy (buf, readIndex, bytes, 0, size);
            if (BitConverter.IsLittleEndian) {
                Array.Reverse (bytes);
            }
            readIndex += size;
            return bytes;
        }
        public override byte readByte () {
            return buf[readIndex++];
        }
        public byte[] copyDiscardBytes(int size) {
            byte[] bytes = new byte[size];
            Array.Copy (buf, readIndex, bytes, 0, size);
            readIndex += size;
            return bytes;
        }
        public override void readBytes (byte[] disbytes, int disstart, int len) {
            int size = disstart + len;
            for (int i = disstart; i < size; i++) {
                disbytes[i] = this.readByte ();
            }
        }

        public override void discardReadBytes () {
            lock (locker) {
                if (readIndex <= 0) return;
                int len = buf.Length - readIndex;
                byte[] bufTemp = new byte[len];
                Array.Copy (buf, readIndex, bufTemp, 0, len);
                buf = bufTemp;
                writeIndex -= readIndex;
                markReadIndex -= readIndex;
                if (markReadIndex < 0) {
                    markReadIndex = readIndex;
                }
                markWriteIndex -= readIndex;
                if (markWriteIndex < 0 ||
                    markWriteIndex < readIndex ||
                    markWriteIndex < markReadIndex) {
                    markWriteIndex = writeIndex;
                }
                readIndex = 0;
            }
        }

        public override void clear () {
            lock (locker) {
                buf = new byte[buf.Length];
                readIndex = 0;
                writeIndex = 0;
                markReadIndex = 0;
                markWriteIndex = 0;
            }
        }

        public override byte[] toArray () {
            byte[] bytes = new byte[writeIndex];
            Array.Copy (buf, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        //Fix cache capacity
        private int fixSizeAndReset (int currLen, int futureLen) {
            if (futureLen > currLen) {
                //Find a min number which is the power of two and large than the origin size
                //Ensure the size base on the twice of this number
                int size = minNumIsPowerOfTwoAndNear (currLen) * 2;
                if (futureLen > size) {
                    //Ensure inner byte cache size base on the twice of future length
                    size = minNumIsPowerOfTwoAndNear (futureLen) * 2;
                }
                byte[] bufTemp = new byte[size];
                Array.Copy (buf, 0, bufTemp, 0, currLen);
                buf = bufTemp;
                capacity = bufTemp.Length;
            }
            return futureLen;
        }

        private int minNumIsPowerOfTwoAndNear (int num) {
            int n = 2, b = 2;
            while (b < num) {
                b = 2 << n;
                n++;
            }
            return b;
        }

    }

}