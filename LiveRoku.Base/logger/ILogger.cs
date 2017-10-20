namespace LiveRoku.Base {
    public interface ILogger {
        LowList<ILogHandler> LogHandlers { get; }
        void log (Level level, string message);
    }
}