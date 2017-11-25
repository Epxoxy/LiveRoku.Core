namespace LiveRoku.Core {
    internal static class GlobalHelper {

        public static void printStackTrace(this System.Exception e, string category = null){
            if (e == null) return;
            System.Diagnostics.Debug.WriteLine(e.ToString(), category);
        }

        public static bool canConnectTo (string hostNameOrAddress) {
            //TODO support timeout
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

        public static string getFriendlyTime (long ms) {
            return new System.Text.StringBuilder ()
                .Append ((ms / (1000 * 60 * 60)).ToString ("00")).Append (":")
                .Append ((ms / (1000 * 60) % 60).ToString ("00")).Append (":")
                .Append ((ms / 1000 % 60).ToString ("00")).ToString ();
        }

        public static double totalMsToGreenTime (this System.DateTime time) {
            return (time - new System.DateTime (1970, 1, 1, 0, 0, 0, 0)).TotalMilliseconds;
        }

    }
}