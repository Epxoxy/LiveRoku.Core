using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LiveRoku.Core {
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
                System.Diagnostics.Debug.WriteLine ("try cancel " + key);
                cancel (exist);
            }
        }
        private void cancel (CancellationTokenSource cts) {
            if (cts?.Token.CanBeCanceled == true) {
                try {
                    cts.Cancel ();
                } catch (Exception e) {
                    e.printStackTrace ();
                }
            }
        }
    }
}