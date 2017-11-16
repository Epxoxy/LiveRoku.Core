namespace LiveRoku.Loader {
    using System;
    using System.Collections.Generic;
    public class LoadContextBase {
        public bool LoadOk { get; internal set; }

        protected string DataDirectory { get; set; }
        protected string AppDataFileName { get; set; }

        public Type CoreType { get; internal set; }
        public IReadOnlyList<Type> PluginTypes { get; internal set; }
        public LoadConfig AppLocalData { get; internal set; }

        public LoadContextBase (string dataDir, string appDataFileName) {
            this.DataDirectory = dataDir;
            this.AppDataFileName = appDataFileName;
        }
    }
}