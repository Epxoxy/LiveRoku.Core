﻿using System.Collections.Generic;
namespace LiveRoku.Loader {
    public class SettingsSection {

        public string AccessKey { get; private set; }
        public Dictionary<string, object> Items { get; private set; }

        public SettingsSection () { }

        public SettingsSection (string accessKey, Dictionary<string, object> items) {
            this.AccessKey = accessKey;
            this.Items = items ?? new Dictionary<string, object> ();
        }

        public SettingsSection (string accessKey) : this (accessKey, null) { }

        public void combineWith (IDictionary<string, object> settings) {
            if (settings == null || settings.Count <= 0) return;
            //Combine settings
            Items = Items ?? (Items = new Dictionary<string, object> ());
            //Wrap settings
            foreach (var pair in settings) {
                if (Items.ContainsKey (pair.Key)) {
                    Items[pair.Key] = pair.Value;
                } else Items.Add (pair.Key, pair.Value);
            }
        }

    }
}