namespace LiveRoku.Loader.Base {
    using System.Collections.Generic;
    using LiveRoku.Base;

    public class AppLocalData {

        internal SettingSection AppSettings { get; } = new SettingSection ("app.settings", null);
        public Dictionary<string, PluginConfig> AppConfigs { get; internal set; } = new Dictionary<string, PluginConfig> ();

        [Newtonsoft.Json.JsonIgnore]
        public Dictionary<string, SettingSection> ExtraSettings { get; internal set; }

        [Newtonsoft.Json.JsonIgnore]
        internal string StoreDir { get; set; }
        [Newtonsoft.Json.JsonIgnore]
        internal string AppDataFileName { get; set; }
        [Newtonsoft.Json.JsonIgnore]
        internal string ExtraFileName { get; set; }

        private ISettings appSettings;
        public AppLocalData () { }

        public AppLocalData (string dataDir, string dataFileName, string extraConfig) {
            this.StoreDir = dataDir;
            this.AppDataFileName = dataFileName;
            this.ExtraFileName = extraConfig;
        }

        public ISettings getAppSettings () {
            return appSettings ?? (appSettings = new EasySettings (AppSettings.Items));
        }
    }
}