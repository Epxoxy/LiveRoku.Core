namespace LiveRoku.Base.Logger {
    public interface ILogHandler {
        void onLog(Level level, string message);
    }
}
