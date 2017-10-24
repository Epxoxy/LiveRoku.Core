using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace LiveRoku.Core {
    //A list wrapper for provider low function
    public class LowList<T> : Base.ILowList<T>, IEnumerable<T> where T : class {
        private readonly ReaderWriterLockSlim locker = new ReaderWriterLockSlim ();
        private List<T> cache = new List<T> ();

        ~LowList () {
            locker?.Dispose ();
        }

        public IEnumerator<T> GetEnumerator() {
            return new ConcurrentEnumerator<T>(cache, locker);
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return new ConcurrentEnumerator<T>(cache, locker);
        }

        public int count() {
            return cache.Count;
        }

        public void add (T value) => modify (() => cache.Add (value));

        public void remove (T value) => modify (() => cache.Remove (value));

        public void purge () => modify (() => cache.RemoveAll (obj => null == obj));

        public void clear () => modify (() => cache.Clear ());

        private void modify (System.Action doWhat) {
            locker.EnterWriteLock ();
            try {
                doWhat.Invoke ();
            } finally {
                locker.ExitWriteLock ();
            }
        }
    }
}