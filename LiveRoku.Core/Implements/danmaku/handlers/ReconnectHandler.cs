namespace LiveRoku.Core.Danmaku {
    using LiveRoku.Core.Common;
    using System;
    using System.Timers;

    public class ReconnectHandler : AbstractFlowResolver {
        public bool ReconnectEnabled { get; private set; } = true;
        public Action InactiveTotally { get; set; }
        public Action HowToReconnect { get; set; }
        private readonly Timer timer = new Timer {
            AutoReset = false
        };
        private int attempts = 0;
        private int maxAttempts = 10;

        public ReconnectHandler() {
            timer.Elapsed += tryReconnect;
        }

        protected override void Dispose(bool disposing) {
            timer.Elapsed -= tryReconnect;
            timer?.Dispose();
        }

        public override void onActive(ITransformContext ctx) {
            attempts = 0;
            timer.Stop();
            base.onActive(ctx);
        }

        public override void onInactive(ITransformContext ctx, object data) {
            timer.Stop();
            if (ReconnectEnabled) {
                if(attempts < maxAttempts) {
                    attempts++;
                    timer.Interval = (400 << attempts);
                    timer.Start();
                } else {
                    InactiveTotally?.Invoke();
                }
            }
            base.onInactive(ctx, data);
        }

        public void doNotReconnect() {
            ReconnectEnabled = false;
            timer.Elapsed -= tryReconnect;
            timer.Stop();
        }

        private void tryReconnect(object sender, ElapsedEventArgs e) {
            //=== step.1 === -delay restart
            //Logger.log(Level.Info, $"Reconnect to danmaku server after {(delay) / 1000d}s");
            System.Diagnostics.Debug.WriteLine($"Attempt reconnect {attempts}.", "reconnect");
            System.Diagnostics.Debug.WriteLine("Reconnect to danmaku server after network test.", "reconnect");
            //=== step.2 === -test network
            if (isConnectable("https://api.live.bilibili.com/api", 5000)) {
                System.Diagnostics.Debug.WriteLine("Network test pass.", "reconnect");
                //== step.3 === -reconnect
                //Logger.log(Level.Info, $"Reconnecting to danmaku server");
                HowToReconnect?.Invoke();
            }
        }

        private bool isConnectable(string address, int timeout) {
            var request = System.Net.WebRequest.Create(address);
            request.Timeout = timeout;
            System.Net.WebResponse response = null;
            try {
                response = request.GetResponse();
                return true;
            } catch {
                return false;
            } finally {
                response?.Dispose();
            }
        }
        
    }
}
