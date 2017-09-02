namespace LiveRoku.Core {
    public class TimeHelper {
        public static double totalMsToGreenTime (System.DateTime time) {
            return (time - new System.DateTime (1970, 1, 1, 0, 0, 0, 0)).TotalMilliseconds;
        }
    }
}