using System;
using System.Collections.Generic;
namespace LiveRoku.Core.Helpers {
    //Weak list wrapper
    //Not useful now
    public class WeakListHost<T> where T : class {
        private List<WeakReference<T>> source;
        private object lockHelper = new object ();

        public WeakListHost () {
            source = new List<WeakReference<T>> ();
        }

        public void add (T value) {
            lock (lockHelper) {
                source.Add (new WeakReference<T> (value));
            }
        }

        public void remove (T value) {
            lock (lockHelper) {
                source.RemoveAll (obj => {
                    T target = null;
                    return (null == obj || (obj.TryGetTarget (out target) && target == value));
                });
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

        public void forEachEx (Action<T> action, Action<Exception> onError) {
            source.ForEach (item => {
                if (null != item) {
                    try {
                        T target = null;
                        if (item.TryGetTarget (out target)) {
                            action.Invoke (target);
                        }
                    } catch (Exception e) {
                        onError.Invoke (e);
                    }
                }
            });
        }
    }
}