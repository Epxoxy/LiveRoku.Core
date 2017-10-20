namespace LiveRoku.Base {
    public interface ILogHandler {
        void onLog(Level level, string message);
    }
}
