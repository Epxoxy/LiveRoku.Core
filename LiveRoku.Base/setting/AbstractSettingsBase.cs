using System.Collections.Generic;
namespace LiveRoku.Base {
    public abstract class AbstractSettingsBase : ISettings {
        public virtual bool CanSaveToDisk { get; protected set; } = false;

        public IReadOnlyDictionary<string, object> Settings => settings;
        private readonly Dictionary<string, object> settings;

        public AbstractSettingsBase() : this(new Dictionary<string, object>()) { }

        public AbstractSettingsBase(Dictionary<string, object> settings) {
            this.settings = settings ?? new Dictionary<string, object>();
        }

        public bool delete(string key) {
            return settings.Remove(key);
        }

        public void put<T>(string key, T value) {
            if (settings.ContainsKey (key)) {
                settings[key] = value;
            } else {
                settings.Add (key, value);
            }
            afterPut(key, value);
        }

        public bool contains(string key) => settings.ContainsKey(key);

        public T get<T>(string key, T defaultValue = default(T)) {
            if(settings.TryGetValue(key, out object value) && value is T) {
                return (T)value;
            }
            return defaultValue;
        }

        public void clear() {
            settings.Clear();
            afterClear();
        }

        protected abstract void afterClear();
        protected abstract void afterPut<T>(string key, T value);
        public abstract bool saveToDisk();
    }
}
