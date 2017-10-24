using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LiveRoku.Core
{
    //see https://stackoverflow.com/questions/6601611/no-concurrentlistt-in-net-4-0
    internal class ConcurrentEnumerator<T> : IEnumerator<T>
    {
        #region Fields
        private readonly IEnumerator<T> mInner;
        private readonly ReaderWriterLockSlim mLock;
        #endregion

        #region Constructor
        public ConcurrentEnumerator(IEnumerable<T> mInner, ReaderWriterLockSlim mLock)
        {
            this.mLock = mLock;
            this.mLock.EnterReadLock();
            this.mInner = mInner.GetEnumerator();
        }
        #endregion

        #region Methods
        public bool MoveNext() => mInner.MoveNext();

        public void Reset() => mInner.Reset();

        public void Dispose() => this.mLock.ExitReadLock();
        #endregion

        #region Properties
        public T Current => mInner.Current;

        object IEnumerator.Current => mInner.Current;
        #endregion
    }
}
