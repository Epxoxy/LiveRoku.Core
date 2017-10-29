using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace LiveRoku.Core {
    //A list wrapper for provider low function
    public class LowList<T> : Base.ILowList<T>, IEnumerable<T> where T : class {
        private List<T> op = new List<T>();
        private T[] cache = new T[0];
        private object locker = new object();
        
        public IEnumerator<T> GetEnumerator() {
            return ((IEnumerable<T>)cache).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return cache.GetEnumerator();
        }

        public void add (T value) => modify (() => op.Add (value));

        public void addRange(IEnumerable<T> collection) => modify(() => op.AddRange(collection));

        public void remove (T value) => modify (() => op.Remove (value));

        public void removeRange(int index, int count) => modify(() => op.RemoveRange(index, count));

        public void purge () => modify (() => op.RemoveAll (obj => null == obj));

        public void clear () => modify (() => op.Clear ());

        private void modify (System.Action doWhat) {
            lock (locker) {
                //invoke action
                try { doWhat.Invoke(); }
                catch (System.Exception e) {
                    e.printStackTrace();
                }
                //copy to cache
                var newCache = new T[op.Count];
                try { op.CopyTo(newCache); }
                catch (System.Exception e) {
                    e.printStackTrace();
                }
                cache = newCache;
            }
        }
    }
}