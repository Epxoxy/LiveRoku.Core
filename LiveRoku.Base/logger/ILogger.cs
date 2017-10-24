namespace LiveRoku.Base.Logger {
    public interface ILogger {
        ILowList<ILogHandler> LogHandlers { get; }
        void log (Level level, string message);
    }
}