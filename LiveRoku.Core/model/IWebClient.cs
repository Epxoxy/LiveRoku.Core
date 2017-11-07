namespace LiveRoku.Core.Models {
    using System;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Threading.Tasks;
    //For PCL
    //TODO Complete it
    public interface IWebClient : IDisposable {
        Encoding DefaultEncoding { get; set; }
        TimeSpan Timeout { get; set; }
        HttpRequestHeaders DefaultRequestHeaders { get; }

        Task<string> GetStringAsyncUsing(string address);
        Task<string> GetStringAsyncUsing(string address, Encoding encoding);
    }
}
