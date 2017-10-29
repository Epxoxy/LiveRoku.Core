namespace LiveRoku.Core {
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using LiveRoku.Base;
    public class DanmakuWriter {
        private const string XmlHeader = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><i>";
        private const string XmlFooter = "</i>";
        public bool IsRunning { get; private set; }
        private ConcurrentQueue<DanmakuModel> danmakuQueue;
        private CancellationTokenSource writting;
        private long baseTime;
        private Encoding encoding;
        private FileStream fs;
        private StreamWriter sWriter;

        public DanmakuWriter (Encoding encoding) {
            danmakuQueue = new ConcurrentQueue<DanmakuModel> ();
            this.encoding = encoding;
        }

        public Task startAsync (string fileFullName, long baseTime) {
            if (IsRunning) {
                return Task.FromResult (false);
            }
            this.IsRunning = true;
            this.baseTime = baseTime;
            danmakuQueue = new ConcurrentQueue<DanmakuModel> ();
            return startWriteAsync (fileFullName);
        }

        public void stop (bool force = false) {
            if (IsRunning) {
                IsRunning = false;
                if (force && writting?.Token.CanBeCanceled == true) {
                    writting.Cancel ();
                }
                var temp = sWriter;
                sWriter = null;
                using (temp) {
                    temp.Write (XmlFooter);
                }
                using (fs) { }
            }
        }

        public void enqueue (DanmakuModel danmaku) {
            if (!IsRunning || danmaku == null || danmaku.MsgType != MsgTypeEnum.Comment) return;
            danmakuQueue.Enqueue (danmaku);
        }

        [SuppressMessage ("Microsoft.Performance", "CS4014")]
        private Task startWriteAsync (string fileFullName) {
            if (writting?.Token.CanBeCanceled == true) {
                writting.Cancel ();
            }
            writting = new CancellationTokenSource ();
            return Task.Run (async () => {
                //PART.1 Write file head part
                try {
                    fs = new FileStream (fileFullName, FileMode.Create);
                    sWriter = new StreamWriter (fs, encoding);
                    sWriter.WriteLine (XmlHeader);
                    sWriter.WriteLine ("<chatserver>chat.bilibili.com</chatserver><chatid>0</chatid>");
                    sWriter.WriteLine ("<mission>0</mission><maxlimit>0</maxlimit><source>k-v</source>");
                } catch (Exception e) {
                    e.printStackTrace();
                }
                //PART2. Start writing danmaku
                int idleTimes = 0, writeTimes = 0;
                while (IsRunning) {
                    try {
                        //1.Wait from something enqueue
                        while (IsRunning && danmakuQueue.IsEmpty) {
                            if (++idleTimes > 5 && writeTimes > 0) {
                                sWriter?.Flush (); //Flush when idle
                            }
                            idleTimes %= 6; //Limit in 0~5
                            await Task.Delay (100, writting.Token);
                        }
                        //2.Trying to dequeue
                        DanmakuModel danmaku = null;
                        while (IsRunning && !danmakuQueue.TryDequeue (out danmaku)) { }
                        if (danmaku == null) continue;
                        //3.Write to stream
                        lock (sWriter) {
                            sWriter.WriteLine (danmaku.ToString (baseTime));
                            if (writeTimes++ > 10) {
                                sWriter.Flush ();
                            }
                        }
                    } catch (Exception e) {
                        e.printStackTrace();
                    }
                }
                try {
                    sWriter?.Flush ();
                } catch (Exception e) {
                    e.printStackTrace();
                }
            }, writting.Token);
        }

    }
}