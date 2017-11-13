namespace LiveRoku.Core.Common {
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    internal class CancellationManager {
        private Dictionary<string, CancellationTokenSource> ctsTemp;
        private object lockHelper = new object ();
        public CancellationManager () {
            ctsTemp = new Dictionary<string, CancellationTokenSource> ();
        }

        public void set (string key, CancellationTokenSource cts) {
            if (ctsTemp.ContainsKey (key)) {
                ctsTemp[key] = cts;
            } else {
                ctsTemp.Add (key, cts);
            }
        }

        public void cancelAll () {
            lock (lockHelper) {
                foreach (var key in ctsTemp.Keys) {
                    cancel (key);
                }
            }
        }

        public void clear () {
            lock (lockHelper) {
                ctsTemp.Clear ();
            }
        }

        public void remove (string key) {
            lock (lockHelper) {
                if (ctsTemp.ContainsKey (key)) {
                    ctsTemp.Remove (key);
                }
            }
        }

        public void cancel (string key) {
            CancellationTokenSource exist = null;
            if (ctsTemp.TryGetValue (key, out exist)) {
                System.Diagnostics.Debug.WriteLine ("try cancel " + key, "cancelMgr");
                cancel (exist);
            }
        }
        private void cancel (CancellationTokenSource cts) {
            if (cts?.Token.CanBeCanceled == true) {
                try {
                    cts.Cancel ();
                    cts.Dispose();
                } catch (Exception e) {
                    e.printStackTrace();
                }
            }
        }
        
        //...........
        //Help method
        //...........
        public Task runOnlyOne(string tokenKey, Action<CancellationToken> action, int timeout = 0, Action onCancelled = null) {
            if (action == null)
                return Task.FromResult(false);
            var cts = timeout > 0 ? new CancellationTokenSource(timeout) :
                new CancellationTokenSource();
            var ctr = cts.Token.Register(() => {
                Debug.WriteLine($"Cancel {tokenKey}", "tasks");
                onCancelled?.Invoke();
            });
            this.cancel(tokenKey);
            this.set(tokenKey, cts);
            return Task.Run(() => {
                try {
                    action.Invoke(cts.Token);
                } catch (Exception e) {
                    e.printStackTrace("cancel-mgr");
                } finally {
                    this.remove(tokenKey);
                    this.cancel(cts);
                    using (ctr) { }
                }
            }, cts.Token);
        }

        public Task runOnlyOne(string tokenKey, Action action, int timeout = 0, Action onCancelled = null) {
            if (action == null)
                return Task.FromResult(false);
            var cts = timeout > 0 ? new CancellationTokenSource(timeout) :
                new CancellationTokenSource();
            var ctr = cts.Token.Register(() => {
                Debug.WriteLine($"Cancel {tokenKey}", "tasks");
                onCancelled?.Invoke();
            });
            this.cancel(tokenKey);
            this.set(tokenKey, cts);
            return Task.Run(() => {
                try {
                    action.Invoke();
                } catch (Exception e) {
                    e.printStackTrace("cancel-mgr");
                } finally {
                    this.remove(tokenKey);
                    this.cancel(cts);
                    using (ctr) { }
                }
            }, cts.Token);
        }

    }
}