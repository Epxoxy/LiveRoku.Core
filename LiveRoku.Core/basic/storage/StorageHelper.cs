using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LiveRoku.Core.Storage {
    public class StorageHelper : Base.IStorage {
        private Dictionary<string, Wrapper> unknowDict;
        private Dictionary<string, object> valueDict;
        private volatile static StorageHelper helper = null;
        private static readonly object lockHelper = new object ();
        private readonly string folder;
        private readonly string txtpath;
        private StorageHelper (string folder) {
            this.folder = folder;
            this.txtpath = folder + "\\settings.txt";
            initialize ();
        }

        public static StorageHelper instance (string folder) {
            if (helper == null) {
                lock (lockHelper) {
                    if (helper == null)
                        helper = new StorageHelper (folder);
                }
            }
            return helper;
        }

        public bool tryGet (string name, out object obj) {
            obj = null;
            if (valueDict != null && valueDict.ContainsKey (name)) {
                obj = valueDict[name];
            }
            if (obj == null)
                return false;
            return true;
        }

        public bool tryGet<T> (string name, out T obj) {
            obj = default (T);
            object exist = null;
            if (tryGet (name, out exist)) {
                if (exist.GetType () == typeof (T)) {
                    obj = (T) exist;
                    return true;
                }
            }
            return false;
        }

        public bool add (string name, object value) {
            if (valueDict != null && !string.IsNullOrEmpty (name)) {
                if (!valueDict.ContainsKey (name)) {
                    valueDict.Add (name, value);
                } else {
                    valueDict[name] = value;
                }
                return true;
            }
            return false;
        }

        public bool save () {
            try {
                List<Wrapper> wrappers = new List<Wrapper> (valueDict.Count);
                foreach (var pair in valueDict) {
                    if (pair.Value == null) continue;
                    if (unknowDict.ContainsKey (pair.Key))
                        unknowDict.Remove (pair.Key);
                    wrappers.Add (new Wrapper (pair.Key, pair.Value));
                }
                foreach (var pair in unknowDict) {
                    wrappers.Add (pair.Value);
                }
                if (wrappers.Count == 0) return true;
                FileHelper.writeTxt (serialize (wrappers.ToArray ()), txtpath);
                return true;
            } catch (Exception e) {
                e.printStackTrace ();
                return false;
            }
        }

        private void initialize () {
            string txt = FileHelper.readTxt (txtpath);
            valueDict = new Dictionary<string, object> ();
            unknowDict = new Dictionary<string, Wrapper> ();
            if (!string.IsNullOrEmpty (txt)) {
                Wrapper[] wrappers = null;
                try {
                    wrappers = deserialize<Wrapper[]> (txt);
                } catch (Exception e) {
                    e.printStackTrace ();
                }
                if (wrappers != null && wrappers.Length > 0) {
                    foreach (var wrapper in wrappers) {
                        var jObject = wrapper.Value as JObject;
                        if (jObject == null) continue;
                        object entity = null;
                        try {
                            var type = Type.GetType (wrapper.TypeName, true, true);
                            if (type != null)
                                entity = jObject.ToObject (type);
                        } catch (Exception e) {
                            e.printStackTrace ();
                        }
                        if (entity == null) {
                            unknowDict.Add (wrapper.Key, wrapper);
                        } else {
                            valueDict.Add (wrapper.Key, entity);
                        }
                    }
                }
            }
        }

        private string serialize (object o) {
            return JsonConvert.SerializeObject (o, Formatting.Indented); //, serializerSettings());
        }
        private T deserialize<T> (string json) {
            return JsonConvert.DeserializeObject<T> (json);
        }

        class Wrapper {
            public string Key { get; set; }
            public string TypeName { get; set; }
            public JObject Value { get; set; }
            public Wrapper () { }
            public Wrapper (string key, object value) {
                this.Key = key;
                this.TypeName = value.GetType ().AssemblyQualifiedName;
                this.Value = JObject.FromObject (value);
            }
        }
        /*private JsonSerializerSettings serializerSettings () {
            return new JsonSerializerSettings {
                TypeNameHandling = TypeNameHandling.All,
                TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
                Error = (sender, args) => { args.ErrorContext.Handled = true; }
            };
        }*/

    }
}