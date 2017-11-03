namespace LiveRoku.Core.Models {
    public class FetchServerResult {
        public string Host { get; set; }
        public int Port { get; set; }
        public bool FetchOK { get; set; }
        public bool MayNotExist { get; set; }
        public bool CanUseDefault { get; internal set; } = true;
        //public bool IsAvailable => MayNotExist || !CanUseDefault;
        public bool FetchOKOrDefault => FetchOK || CanUseDefault;
    }
}
