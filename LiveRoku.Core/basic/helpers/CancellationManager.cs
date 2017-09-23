using System;
using System.Collections.Generic;
using System.Threading;

namespace LiveRoku.Core {
    internal class CancellationManager {
        private Dictionary<string, CancellationTokenSource> ctsTemp;
        public CancellationManager() {
            ctsTemp = new Dictionary<string, CancellationTokenSource>();
        }

        public void set (string key, CancellationTokenSource cts) {
            if (ctsTemp.ContainsKey (key)) {
                ctsTemp[key] = cts;
            } else {
                ctsTemp.Add (key, cts);
            }
        }

        public void remove (string key) {
            if (ctsTemp.ContainsKey (key)) {
                ctsTemp.Remove (key);
            }
        }
        public void cancel (string key) {
            CancellationTokenSource exist = null;
            if (ctsTemp.TryGetValue (key, out exist)) {
                System.Diagnostics.Debug.WriteLine("try cancel " + key);
                cancel (exist);
            }
        }
        private void cancel (CancellationTokenSource cts) {
            if (cts.Token.CanBeCanceled) {
                try {
                    cts.Cancel ();
                } catch (Exception e) {
                    e.printStackTrace ();
                }
            }
        }
    }
}