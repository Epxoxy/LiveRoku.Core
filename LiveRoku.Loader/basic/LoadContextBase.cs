using System;
using System.Collections.Generic;
namespace LiveRoku.Loader {
    public class LoadContextBase {
        public bool LoadOk { get; internal set; }

        protected string DataDirectory { get; set; }
        protected string AppDataFileName { get; set; }

        public Type CoreType { get; internal set; }
        public IReadOnlyList<Type> PluginTypes { get; internal set; }
        public ContextLoadConfig AppLocalData { get; internal set; }

        public LoadContextBase (string dataDir, string appDataFileName) {
            this.DataDirectory = dataDir;
            this.AppDataFileName = appDataFileName;
        }
    }
}