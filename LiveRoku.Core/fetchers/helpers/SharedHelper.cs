using System;
using System.Threading.Tasks;

namespace LiveRoku.Core {
    internal static class SharedHelper {
        public static bool checkCanConnect (string hostNameOrAddress) {
            try {
                System.Net.Dns.GetHostEntry (hostNameOrAddress);
                return true;
            } catch { //Exception message is not a important part here
                return false;
            }
        }
        public static void printOn (this Exception e, Base.ILogger logger) {
            if (e == null) return;
            e.printStackTrace ();
            logger.log (Base.Level.Error, e.Message);
        }
        public static void forEachHideExAsync<T> (this Base.LowList<T> host, Action<T> action, Base.ILogger logger) where T : class {
            host.forEachSafelyAsync (action, error => {
                System.Diagnostics.Debug.WriteLine ($"[{typeof (T).Name}]-" + error.Message);
            }).ContinueWith (task => {
                task.Exception?.printOn (logger);
            }, TaskContinuationOptions.OnlyOnFaulted);
        }
        public static string getFriendlyTime (long ms) {
            return new System.Text.StringBuilder ()
                .Append ((ms / (1000 * 60 * 60)).ToString ("00")).Append (":")
                .Append ((ms / (1000 * 60) % 60).ToString ("00")).Append (":")
                .Append ((ms / 1000 % 60).ToString ("00")).ToString ();
        }
    }
}