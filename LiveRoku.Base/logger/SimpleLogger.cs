using System.Diagnostics;
using System.Threading.Tasks;

namespace LiveRoku.Base {
    public class SimpleLogger : ILogger {
        public LowList<ILogHandler> LogHandlers { get; private set; }

        public SimpleLogger() { LogHandlers = new LowList<ILogHandler>(); }

        public void log(Level level, string message) {
            LogHandlers.forEachSafelyAsync(handler => {
                handler.onLog(level, message);
            }, e => {
                Debug.WriteLine($"[{LogHandlers.GetType().Name}]-" + e.Message);
            }).ContinueWith(task => {
                Debug.WriteLine(task.Exception);
            }, TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
