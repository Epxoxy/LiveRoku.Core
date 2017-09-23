using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using LiveRoku.Base;
using System.Diagnostics.CodeAnalysis;

namespace LiveRoku.Core {
    public class DanmakuStorage {
        private const string XmlHeader = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><i><chatserver>chat.bilibili.com</chatserver><chatid>0</chatid><mission>0</mission><maxlimit>0</maxlimit><source>k-v</source>";
        private const string XmlFooter = "</i>";
        private readonly ConcurrentQueue<DanmakuModel> danmakuQueue;
        private string storagePath;
        private long nowTime;
        private int flushTime;

        private Encoding encoding;
        private FileStream writerFs;
        private StreamWriter writer;
        private object locker = new object();
        public bool IsWriting { get; private set; }

        public DanmakuStorage (string storagePath, long nowTime, Encoding encoding, int flushTime = 30000) {
            danmakuQueue = new ConcurrentQueue<DanmakuModel> ();
            this.storagePath = storagePath;
            this.nowTime = nowTime;
            this.flushTime = flushTime;
            this.encoding = encoding;
        }

        public void startAsync () {
            if (IsWriting) return;
            IsWriting = true;
            startWrite();
        }

        public void stop (bool force = false) {
            IsWriting = false;
            if (force) {
                try {
                    writer.Close ();
                    writerFs.Close ();
                } catch (Exception e) {
                    e.printStackTrace ();
                }
            }
        }

        public void enqueue(DanmakuModel danmaku) {
            if (danmaku == null || danmaku.MsgType != MsgTypeEnum.Comment) return;
            danmakuQueue.Enqueue (danmaku);
        }

        [SuppressMessage("Microsoft.Performance", "CS4014")]
        private async void startWrite () {
            try {
                writerFs = new FileStream (storagePath, FileMode.Create);
                writer = new StreamWriter (writerFs, encoding);
                writer.Write (XmlHeader);
            } catch (Exception e) {
                e.printStackTrace ();
            }

            Task.Run(async () => {
                while (IsWriting) {
                    if (writer == null) break;
                    lock (locker) {
                        writer.Flush();
                    }
                    await Task.Delay(flushTime);
                }
            }).ContinueWith(task => {
                task.Exception?.printStackTrace();
            }, TaskContinuationOptions.OnlyOnFaulted);
            while (IsWriting) {
                try {
                    var danmaku = await dequeue ();
                    if (danmaku == null) continue;
                    //TODO implements danmakuModel.ToString(datetime) method
                    lock (locker) {
                        writer.WriteLine (danmaku.ToString (nowTime));
                    }
                } catch (Exception e) {
                    e.printStackTrace ();
                    continue;
                }
            }
            try {
                writer.Write (XmlFooter);
                writer.Flush ();
                writer.Close ();
                writerFs.Close ();
            } catch (Exception e) {
                e.printStackTrace ();
            }
        }

        private async Task<DanmakuModel> dequeue () {
            DanmakuModel result = null;
            while (danmakuQueue.IsEmpty) {
                await Task.Delay (100);
            }
            while (!danmakuQueue.TryDequeue(out result)) { }
            return result;
        }
        
    }
}