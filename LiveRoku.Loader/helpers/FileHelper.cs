namespace LiveRoku.Loader.Helpers {
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Newtonsoft.Json;

    public class FileHelper {

        public static IEnumerable<string> safelyGetFiles (string dir, string searchPattern) {
            return Directory.Exists (dir) ? Directory.EnumerateFiles (dir, searchPattern) : Enumerable.Empty<string> ();
        }

        public static void writeText (string text, string path, bool append = false) {
            writeText (text, path, Encoding.UTF8, append);
        }

        public static void writeText (string text, string path, Encoding encoding, bool append = false) {
            var file = new FileInfo (path);
            if (!file.Directory.Exists) {
                Directory.CreateDirectory (file.Directory.FullName);
            }
            FileMode mode = append && file.Exists ? FileMode.Append : FileMode.Create;

            try {
                using (var fs = new FileStream (path, mode, FileAccess.Write, FileShare.Write)) {
                    using (var writer = new StreamWriter (fs, encoding)) {
                        writer.Write (text);
                    }
                }
            } catch (System.Exception e) {
                System.Diagnostics.Debug.WriteLine (e.ToString ());
            }
        }

        public static string readText (string path) {
            return readText (path, Encoding.UTF8);
        }

        public static string readText (string path, Encoding encoding) {
            var file = new FileInfo (path);
            if (!file.Directory.Exists || !file.Exists) {
                return string.Empty;
            }
            try {
                using (var fs = new FileStream (path, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                    using (var reader = new StreamReader (fs, encoding)) {
                        return reader.ReadToEnd ();
                    }
                }
            } catch (System.Exception e) {
                System.Diagnostics.Debug.WriteLine (e.ToString ());
            }
            return string.Empty;
        }

        public static bool serializeToLocal<T> (T obj, string path) {
            try {
                var text = FileHelper.serializeToJson (obj);
                FileHelper.writeText (text, path);
            } catch (System.Exception e) {
                System.Diagnostics.Debug.WriteLine (e.ToString ());
                return false;
            }
            return true;
        }

        public static T deserializeFromPath<T> (string path) {
            try {
                var text = FileHelper.readText (path);
                return FileHelper.deserializeFromJson<T> (text);
            } catch (System.Exception e) {
                System.Diagnostics.Debug.WriteLine (e.ToString ());
            }
            return default (T);
        }

        public static string serializeToJson (object o) {
            return JsonConvert.SerializeObject (o, Formatting.Indented, myDefaultSettings);
        }

        public static string serializeToJson (object o, JsonSerializerSettings setting) {
            return JsonConvert.SerializeObject (o, Formatting.Indented, setting); //serializerSettings());
        }

        public static T deserializeFromJson<T> (string json) {
            return deserializeFromJson<T> (json, myDefaultSettings);
        }

        public static T deserializeFromJson<T> (string json, JsonSerializerSettings setting) {
            if (string.IsNullOrEmpty (json))
                return default (T);
            return JsonConvert.DeserializeObject<T> (json, setting);
        }

        public static readonly JsonSerializerSettings myDefaultSettings = new JsonSerializerSettings () {
            /*DefaultValueHandling = DefaultValueHandling.Include,*/
            ContractResolver = new NonPublicPropertiesContractResolver (),
            TypeNameHandling = TypeNameHandling.Auto,
            TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple
        };
    }

}