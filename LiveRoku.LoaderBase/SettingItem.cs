using System.Collections.Generic;
namespace LiveRoku.LoaderBase {
    public class SettingItem {

        public string ItemKey { get; set; }
        public object Value { get; set; }

        public SettingItem () { }
        public SettingItem (string itemKey, object value) {
            this.ItemKey = itemKey;
            this.Value = value;
        }

        public static Dictionary<string, object> unwrap(List<SettingItem> items) {
            var exist = new Dictionary<string, object>();
            if (items != null && items.Count > 0) {
                foreach (var item in items) {
                    exist.Add (item.ItemKey, item.Value);
                }
            }
            return exist;
        }
    }
}