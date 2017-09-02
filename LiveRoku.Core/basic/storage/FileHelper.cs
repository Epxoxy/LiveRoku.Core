namespace LiveRoku.Core.Storage {
    using System.IO;
    using System.Text;
    public class FileHelper {
        public static void writeTxt (string txt, string path, bool append = false) {
            writeTxt (txt, path, Encoding.UTF8, append);
        }
        public static void writeTxt (string txt, string path, Encoding encoding, bool append = false) {
            var file = new FileInfo (path);
            if (!file.Directory.Exists) {
                Directory.CreateDirectory (file.Directory.FullName);
            }
            FileMode mode = append && file.Exists ? FileMode.Append : FileMode.Create;

            try {
                using (var fs = new FileStream (path, mode, FileAccess.Write, FileShare.Write)) {
                    using (var writer = new StreamWriter (fs, encoding)) {
                        writer.Write (txt);
                    }
                }
            } catch (System.Exception e) {
                e.printStackTrace ();
            }
        }
        public static string readTxt (string path) {
            return readTxt (path, Encoding.UTF8);
        }
        public static string readTxt (string path, Encoding encoding) {
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
                e.printStackTrace ();
            }
            return string.Empty;
        }
    }
}