using System;
using System.Collections.Generic;

namespace LiveRoku.Core {
    public static class WeakHelper {
        public static void forEach<T> (this List<WeakReference<T>> list, Action<T> action, Action<string> onException) where T : class {
            List<WeakReference<T>> nullList = new List<WeakReference<T>> ();
            list.ForEach (item => {
                try {
                    T target = null;
                    if (item.TryGetTarget (out target)) {
                        action.Invoke (target);
                    } else {
                        nullList.Add (item);
                    }
                } catch (Exception e) {
                    onException.Invoke (e.Message);
                }
            });
            foreach (var obj in nullList) {
                list.Remove (obj);
            }
        }
    }
}