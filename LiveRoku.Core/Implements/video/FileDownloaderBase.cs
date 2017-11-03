namespace LiveRoku.Core {
    using System;
    using System.ComponentModel;
    using System.IO;
    using System.Net;
    using System.Threading.Tasks;

    internal abstract class FileDownloaderBase {
        public bool IsRunning { get; private set; }
        public bool IsBusy => client?.IsBusy == true;

        private WebClient client;
        protected string savePath;

        public FileDownloaderBase (string savePath) {
            this.savePath = savePath;
        }

        public bool updateSavePath (string savePath) {
            if (IsRunning) return false;
            this.savePath = savePath;
            return true;
        }

        public Task<bool> startAsync (string uri) {
            if (IsRunning || !checkFolder()) return Task.FromResult(false);
            IsRunning = true;
            onStarting ();
            client = new WebClient ();
            initClient (client);
            client.DownloadFileCompleted += stopDownload;
            client.DownloadProgressChanged += showProgress;
            return client.DownloadFileTaskAsync (new Uri (uri), savePath).ContinueWith (task => {
                stopAsync ();
                onDownloadEnded();
                task.Exception?.printStackTrace ();
                System.Diagnostics.Debug.WriteLine("Download task completed.", "dloader");
                return true;
            });
        }

        public void stopAsync () {
            if (IsRunning) {
                IsRunning = false;
                if (client != null) {
                    using (var temp = client) {
                        client = null;
                        temp.DownloadFileCompleted -= stopDownload;
                        temp.DownloadProgressChanged -= showProgress;
                        temp.CancelAsync();
                        temp.Dispose();
                    }
                }
                onStopped ();
            }
        }

        private void stopDownload (object sender, AsyncCompletedEventArgs e) {
            stopAsync ();
        }

        private void showProgress (object sender, DownloadProgressChangedEventArgs e) {
            onProgressUpdate (e);
        }

        private bool checkFolder () {
            if (string.IsNullOrEmpty (savePath))
                return false;
            if (Directory.Exists (Path.GetDirectoryName (savePath)))
                return true;
            try {
                Directory.CreateDirectory (Path.GetDirectoryName (savePath));
                return true;
            } catch (Exception e) {
                e.printStackTrace();
                stopAsync ();
                return false;
            }
        }

        protected abstract void onStarting();
        protected abstract void onStopped ();
        protected virtual void onDownloadEnded() { }
        protected abstract void onProgressUpdate (DownloadProgressChangedEventArgs e);
        protected abstract void initClient (WebClient client);
    }
}