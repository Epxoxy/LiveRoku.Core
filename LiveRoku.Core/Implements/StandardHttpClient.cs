namespace LiveRoku.Core {
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;

    class StandardHttpClient : HttpClient, LiveRoku.Core.Models.IWebClient {
        public Encoding DefaultEncoding { get; set; }

        public Task<string> GetStringAsyncUsing(string address) {
            return GetStringAsyncUsing(address, DefaultEncoding ?? Encoding.UTF8);
        }
        public async Task<string> GetStringAsyncUsing(string address, Encoding encoding) {
            var response = await this.GetByteArrayAsync(address);
            return encoding.GetString(response);
        }
    }
}
