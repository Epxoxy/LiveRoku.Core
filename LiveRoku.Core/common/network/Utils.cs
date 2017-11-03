namespace LiveRoku.Core.Common {
    internal class Utils {
        public static bool canConnectTo (string hostNameOrAddress) {
            try {
                System.Net.Dns.GetHostEntry (hostNameOrAddress);
                return true;
            } catch (System.Exception e) {
                System.Diagnostics.Debug.WriteLine (e.ToString ());
                return false;
            }
        }
    }
}