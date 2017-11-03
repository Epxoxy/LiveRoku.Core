namespace LiveRoku.Core.Models {
    using System;
    using System.Text;
    using System.Threading.Tasks;
    //For PCL
    //TODO Complete it
    public interface IWebClient : IDisposable {
        Encoding Encoding { get; set; }
        string DownloadString(string address);
        void AddHeader(string value);
        void CancelAsync();
        Task DownloadFileTaskAsync(Uri address, string fileName);
    }
}
