using System;
using System.Collections.Generic;
namespace LiveRoku.LoaderBase{
    public class LoadContextBase {
        public bool LoadOk { get; set; }

        public string BaseDirectory { get; protected set; }
        public string DataDirectory { get; protected set; }
        public string ConfigPath { get; protected set; }
        public string ExtraPath { get; protected set; }

        public Type CoreType { get; internal set; }
        public IReadOnlyList<Type> PluginImpls { get; internal set; }
        internal Dictionary<string, SettingItemCollection> AllSettings { get; set; }
        public Dictionary<string, PluginConfiguration> InitConfigs { get; internal set; }
        public SettingItemCollection Extra { get; } = new SettingItemCollection();

        public LoadContextBase(string baseDir, string dataDir, string configPath, string extraPath) {
            this.BaseDirectory = baseDir;
            this.DataDirectory = dataDir;
            this.ConfigPath = configPath;
        }
    }
}
