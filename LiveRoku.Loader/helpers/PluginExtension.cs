using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LiveRoku.Base.Plugin;
namespace LiveRoku.Loader {
    internal class PluginHelper {

        public static IDictionary<string, object> findSettings (object instance) {
            if (instance == null) return null;
            var settings = new Dictionary<string, object> ();
            var props = instance.GetType ().GetProperties ();
            foreach (var prop in props) {
                var attributes = prop.GetCustomAttributes (typeof (PluginSettingAttribute), true).OfType<PluginSettingAttribute> ();
                var target = attributes.FirstOrDefault ();
                if (target == null) continue;
                var name = target.Key ?? $"{prop.ReflectedType}.{prop.Name}";
                var value = prop.GetValue (instance);
                settings.Add (name, value);
            }
            return settings;
        }

        public static void applySettings (object instance, IDictionary<string, object> settings) {
            if (instance == null || settings == null || settings.Count <= 0) return;
            var props = instance.GetType ().GetProperties ();
            foreach (var prop in props) {
                var attributes = prop.GetCustomAttributes (typeof (PluginSettingAttribute), true).OfType<PluginSettingAttribute> ();
                var target = attributes.FirstOrDefault ();
                if (target == null) continue;
                var name = target.Key ?? $"{prop.ReflectedType}.{prop.Name}";
                if (settings.TryGetValue (name, out object setting)) {
                    try {
                        prop.SetValue (instance, setting, null);
                    } catch (Exception e) {
                        throw e;
                    }
                } else if (target.Required) {
                    throw new Exception ($"values not found of which is required, name: {name}.");
                }
            }
        }

    }
}