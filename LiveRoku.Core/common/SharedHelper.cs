namespace LiveRoku.Core {
    internal static class SharedHelper {
        public static void printStackTrace(this System.Exception e, string category = null){
            if (e == null) return;
            System.Diagnostics.Debug.WriteLine(e.ToString(), category);
        }

        public static bool checkCanConnect (string hostNameOrAddress) {
            try {
                System.Net.Dns.GetHostEntry (hostNameOrAddress);
                return true;
            } catch { //Exception message is not a important part here
                return false;
            }
        }
        public static void printOn (this System.Exception e, Base.Logger.ILogger logger) {
            if (e == null) return;
            e.printStackTrace();
            logger.log (Base.Logger.Level.Error, e.Message);
        }


        public static void clear<T> (this System.Collections.Concurrent.ConcurrentBag<T> cb) {
            T temp = default (T);
            while (!cb.IsEmpty) {
                cb.TryTake (out temp);
            }
        }

        public static string getFriendlyTime (long ms) {
            return new System.Text.StringBuilder ()
                .Append ((ms / (1000 * 60 * 60)).ToString ("00")).Append (":")
                .Append ((ms / (1000 * 60) % 60).ToString ("00")).Append (":")
                .Append ((ms / 1000 % 60).ToString ("00")).ToString ();
        }

        public static double totalMsToGreenTime (this System.DateTime time) {
            return (time - new System.DateTime (1970, 1, 1, 0, 0, 0, 0)).TotalMilliseconds;
        }

        public static void put<T1, T2> (this System.Collections.Generic.Dictionary<T1, T2> dict, T1 key, T2 value) {
            if (dict.ContainsKey (key)) {
                dict[key] = value;
            } else {
                dict.Add (key, value);
            }
        }
    }
}