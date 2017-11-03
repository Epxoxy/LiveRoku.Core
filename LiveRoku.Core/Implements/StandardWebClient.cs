namespace LiveRoku.Core {
    using System;
    using System.Net;
    internal class StandardWebClient : WebClient, LiveRoku.Core.Models.IWebClient {
        public int RequestTimeout { get; set; } = 100000;

        public void AddHeader(string header) => this.Headers.Add(header);

        protected override WebRequest GetWebRequest(Uri address) {
            var request = base.GetWebRequest(address);
            request.Timeout = RequestTimeout;
            return request;
        }
    }

}
