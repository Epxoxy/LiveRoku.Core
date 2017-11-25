namespace LiveRoku.Core.Common.Helpers {
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
                foreach (var value in ctsTemp.Values) {
                    cancel(value);
                }
                ctsTemp.Clear();
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

        public void cancelAndRemove (string key) {
            CancellationTokenSource exist = null;
            if (ctsTemp.TryGetValue (key, out exist)) {
                System.Diagnostics.Debug.WriteLine ("try cancel " + key, "cancelMgr");
                ctsTemp.Remove(key);
                cancel (exist);
            }
        }

        private void cancel (CancellationTokenSource cts) {
            try {
                if (cts?.Token.CanBeCanceled == true) {
                    cts.Cancel();
                    cts.Dispose();
                }
            } catch (Exception e) {
                e.printStackTrace("cancel-impl");
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
            this.cancelAndRemove(tokenKey);
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
            this.cancelAndRemove(tokenKey);
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