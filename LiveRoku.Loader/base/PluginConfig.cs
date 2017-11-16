namespace LiveRoku.Loader.Base {
    public class PluginConfig {
        public System.Type HostType { get; set; }
        public bool IsEnable { get; set; } = true;
        public string ConfigName { get; set; }
        public string AccessToken { get; set; }
        public int Priority { get; set; }
    }
}