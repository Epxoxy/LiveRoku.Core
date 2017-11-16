namespace LiveRoku.Loader.Base {
    using System;
    using System.Collections.Generic;
    public class ModuleContextBase {
        protected string DataDirectory { get; set; }
        protected string AppDataFileName { get; set; }

        public Type CoreType { get; internal set; }
        public IReadOnlyList<Type> PluginTypes { get; internal set; }
        public AppLocalData AppLocalData { get; internal set; }

        public ModuleContextBase (string dataDir, string appDataFileName) {
            this.DataDirectory = dataDir;
            this.AppDataFileName = appDataFileName;
        }
    }
}