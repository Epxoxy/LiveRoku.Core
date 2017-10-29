namespace LiveRoku.Core {
    using System;
    using System.Text;
    using System.Threading.Tasks;
    //For PCL
    //TODO Complete it
    public interface IWebClient : IDisposable {
        Encoding Encoding { get; set; }
        string DownloadString (string address);
        void AddHeader (string value);
        void CancelAsync ();
        Task DownloadFileTaskAsync (Uri address, string fileName);
    }

    internal class StandardWebClient : IWebClient {
        private readonly System.Net.WebClient webClient = new System.Net.WebClient ();
        public Encoding Encoding {
            get { return webClient.Encoding; }
            set { webClient.Encoding = value; }
        }

        public void AddHeader (string header) => webClient.Headers.Add (header);

        public string DownloadString (string address) => webClient.DownloadString (address);
        public Task DownloadFileTaskAsync (Uri address, string fileName) {
            return webClient.DownloadFileTaskAsync (address, fileName);
        }

        public void CancelAsync () => webClient.CancelAsync ();
        public void Dispose () => webClient.Dispose ();
    }
}