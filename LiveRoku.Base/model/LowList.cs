using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace LiveRoku.Base {
    //A list wrapper for provider low function
    public class LowList<T> where T : class {
        private List<T> source;
        private object lockHelper = new object ();

        public LowList () {
            source = new List<T> ();
        }

        public void add (T value) {
            lock (lockHelper) {
                source.Add (value);
            }
        }

        public void remove (T value) {
            lock (lockHelper) {
                source.Remove (value);
            }
        }

        public void purge () {
            lock (lockHelper) {
                source.RemoveAll (obj => null == obj);
            }
        }

        public void clear () {
            source.Clear ();
        }

        public void forEachSafely (Action<T> action, Action<Exception> onError) {
            source.ForEach (target => {
                if (null != target) {
                    try {
                        action.Invoke (target);
                    } catch (Exception e) {
                        onError.Invoke (e);
                    }
                }
            });
        }

        public Task forEachSafelyAsync(Action<T> action, Action<Exception> onError) {
            return Task.Run(() => forEachSafely(action, onError));
        }
    }
}