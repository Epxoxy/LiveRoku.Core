using System;
using System.IO;

namespace LiveRoku.Core.Storage {
    public class SerializeHelper {
        /// <summary>
        /// Serialize object to path
        /// </summary>
        /// <param name="path">Target path</param>
        /// <param name="sObject">Serialize object</param>
        public static void serializeObject (string path, object sObject) {
            try {
                FileInfo fileInfo = new FileInfo (path);
                if (!fileInfo.Directory.Exists)
                    Directory.CreateDirectory (fileInfo.Directory.FullName);
                //Open or create stream
                using (FileStream fs = new FileStream (path, FileMode.Truncate, FileAccess.ReadWrite, FileShare.Write)) {
                    //Use BinaryFormatter to write and save data
                    System.Runtime.Serialization.Formatters.Binary.BinaryFormatter bf = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter ();
                    bf.Serialize (fs, sObject);
                }
            } catch (Exception e) {
                e.printStackTrace ();
            }
        }

        /// <summary>
        /// Deserialize object from path
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static object deserializeObject (string path) {
            object dObject;
            try {
                //Read the path and create it if it doesn't exits
                FileInfo fileInfo = new FileInfo (path);
                if (!fileInfo.Directory.Exists)
                    Directory.CreateDirectory (fileInfo.Directory.FullName);
                using (FileStream fs = new FileStream (path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Write)) {
                    //Deserialize process
                    System.Runtime.Serialization.Formatters.Binary.BinaryFormatter bf = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter ();
                    dObject = bf.Deserialize (fs);
                }
                return dObject;
            } catch (Exception e) {
                e.printStackTrace ();
            }
            return null;
        }

        private static string defaultPath = AppDomain.CurrentDomain.BaseDirectory + "Configs\\configs.dat";
    }
}