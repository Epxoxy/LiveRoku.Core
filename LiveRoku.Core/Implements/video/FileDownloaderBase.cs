namespace LiveRoku.Core {
    using System;
    using System.ComponentModel;
    using System.IO;
    using System.Net;
    using System.Threading.Tasks;
    internal abstract class FileDownloaderBase {
        public bool IsRunning { get; private set; }
        public Action OnDownloadCompleted;

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

        public Task startAsync (string uri) {
            if (IsRunning) return Task.FromResult (false);
            IsRunning = true;
            onStarting ();
            client = new WebClient ();
            initClient (client);
            client.DownloadFileCompleted += stopDownload;
            client.DownloadProgressChanged += showProgress;
            //TODO Ensure Path Created. 
            if (!checkFolder ()) {
                stop ();
                return Task.FromResult (false);
            }
            return client.DownloadFileTaskAsync (new Uri (uri), savePath).ContinueWith (task => {
                stop ();
                OnDownloadCompleted?.Invoke ();
                task.Exception?.printStackTrace ();
            });
        }

        public void stop () {
            if (IsRunning) {
                IsRunning = false;
                if (client != null) {
                    var temp = client;
                    client = null;
                    temp.DownloadFileCompleted -= stopDownload;
                    temp.DownloadProgressChanged -= showProgress;
                    temp.CancelAsync ();
                    temp.Dispose ();
                }
                onStopped ();
            }
        }

        private void stopDownload (object sender, AsyncCompletedEventArgs e) {
            stop ();
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
                stop ();
                return false;
            }
        }

        protected abstract void onStarting ();
        protected abstract void onStopped ();
        protected abstract void onProgressUpdate (DownloadProgressChangedEventArgs e);
        protected abstract void initClient (WebClient client);
    }
}