namespace LiveRoku.Base {
    public class EasySettings : AbstractSettingsBase {
        protected override void afterClear() { }
        protected override void afterPut<T>(string key, T value) { }
        public override bool saveToDisk() { return false; }
    }
}
