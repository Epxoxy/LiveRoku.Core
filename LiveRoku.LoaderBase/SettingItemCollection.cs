using System.Collections.Generic;
namespace LiveRoku.LoaderBase {
    public class SettingItemCollection  {
        
        public string AccessKey { get; set; }
        public List<SettingItem> Items { get; set; }
        //Internal properties
        internal Dictionary<string, object> UnwarppedSettings { get; set; }

        public SettingItemCollection() { }

        public SettingItemCollection(string accessKey, List<SettingItem> items){
            this.AccessKey = accessKey;
            this.Items = items ?? new List<SettingItem>();
        }

        public SettingItemCollection(string accessKey) : this(accessKey, null) { }
        
        public void combineWith (IDictionary<string, object> settings) {
            if (settings == null || settings.Count <= 0) return;
            //Combine settings
            Items = Items ?? (Items = new List<SettingItem> ());
            for (int i = 0; i < Items.Count; i++) {
                var key = Items[i].ItemKey;
                if (settings.TryGetValue (key, out object value)) {
                    Items[i] = new SettingItem (key, value);
                    settings.Remove (key);
                }
            }
            //Wrap settings
            foreach (var pair in settings)
                Items.Add (new SettingItem (pair.Key, pair.Value));
        }
        
    }
}