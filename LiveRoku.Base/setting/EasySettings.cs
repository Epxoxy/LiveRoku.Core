using System.Collections.Generic;

namespace LiveRoku.Base {
    public class EasySettings : AbstractSettingsBase {
        public EasySettings() { }
        public EasySettings(Dictionary<string, object> setttings) : base(setttings) { }
        protected override void afterClear() { }
        protected override void afterPut<T>(string key, T value) { }
        public override bool saveToDisk() { return false; }
    }
}
