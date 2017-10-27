using System.Collections.Generic;
using System.IO;
using LiveRoku.Base;
using LiveRoku.Base.Plugin;


namespace LiveRoku.Loader {
    public class LoadContext : LoadContextBase, IPluginHost {
        public List<IPlugin> Plugins { get; internal set; }
        public ILiveFetcher Fetcher { get; internal set; }

        public LoadContext (string dataDir, string appDataFileName) : base (dataDir, appDataFileName) { }

        public void saveAppData () {
            saveAppConfigs ();
            foreach (var plugin in Plugins) {
                if (!Plugins.Contains (plugin)) continue;
                var config = AppLocalData.AppConfigs[plugin.GetType ().FullName];
                saveSettingsOf (plugin, config);
            }
        }

        public bool saveAppConfigs () {
            return FileHelper.serializeToLocal (AppLocalData, Path.Combine (DataDirectory, AppDataFileName));
        }

        public bool saveSettingsOf (IPlugin plugin, PluginConfig config) {
            var settings = PluginHelper.findSettings (plugin);
            SettingsSection collection = null;
            if (AppLocalData.ExtraSettings == null) {
                AppLocalData.ExtraSettings = new Dictionary<string, SettingsSection> ();
            }
            var fileName = config.ConfigName;
            var path = System.IO.Path.Combine (DataDirectory, fileName);
            if (!AppLocalData.ExtraSettings.TryGetValue (config.AccessToken, out collection)) {
                collection = new SettingsSection (config.AccessToken);
                AppLocalData.ExtraSettings.Add (config.AccessToken, collection);
            }
            collection.combineWith (settings);
            return FileHelper.serializeToLocal (collection, path);
        }

    }
}