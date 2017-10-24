using System.Collections.Generic;
using LiveRoku.Base;
using LiveRoku.Base.Plugin;
using System;
namespace LiveRoku.LoaderBase {
    public class LoadContext : LoadContextBase, IPluginHost {
        public List<IPlugin> Plugins { get; internal set; }
        public ILiveFetcher Fetcher { get; internal set; }

        public LoadContext (string baseDir, string dataDir, string configPath, string extraPath) 
            : base(baseDir, dataDir, configPath, extraPath) { }
        
        public void saveAllSettings() {
            saveConfigurations();
            foreach (var plugin in Plugins) {
                saveSettingsOf(plugin);
            }
        }

        //TODO Implement save/read
        public bool saveExtra() {
            try {
                FileHelper.writeText(FileHelper.serializeToJson(Extra), ConfigPath);
            } catch (Exception e) {
                System.Diagnostics.Debug.WriteLine(e.ToString());
                return false;
            }
            return true;
        }
        
        public bool saveConfigurations() {
            try {
                FileHelper.writeText(FileHelper.serializeToJson(InitConfigs), ConfigPath);
            } catch (Exception e) {
                System.Diagnostics.Debug.WriteLine(e.ToString());
                return false;
            }
            return true;
        }

        public bool saveSettingsOf (IPlugin plugin) {
            if (!Plugins.Contains (plugin)) return false;
            var config = InitConfigs[plugin.GetType().FullName];
            var settings = PluginHelper.findSettings (plugin);
            SettingItemCollection collection = null;
            if (AllSettings == null) {
                AllSettings = new Dictionary<string, SettingItemCollection>();
            }
            var fileName = config.ConfigName;
            var path = System.IO.Path.Combine(DataDirectory, fileName);
            if (!AllSettings.TryGetValue (config.AccessToken, out collection)) {
                collection = new SettingItemCollection (config.AccessToken);
                AllSettings.Add(config.AccessToken, collection);
            }
            collection.combineWith(settings);
            try {
                //Serialize
                var data = FileHelper.serializeToJson(collection);
                FileHelper.writeText(data, path);
            } catch (Exception e) {
                System.Diagnostics.Debug.WriteLine(e.ToString());
                return false;
            }
            return true;
        }
    }
}