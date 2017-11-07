namespace LiveRoku.Core.Common {
    class LatestAwaitable {
        internal int RequestTimes => requestTimes;//Test

        private object locker = new object();
        private bool someoneRegister = false;
        private int requestTimes = 0;

        public bool addAndRegister() {
            lock (locker) {
                requestTimes++;
                if (someoneRegister)
                    return false;
                someoneRegister = true;
                return true;
            }
        }

        public bool continueRegister() {
            lock (locker) {
                bool canContinue = false;
                if (someoneRegister) {
                    canContinue = requestTimes > 0;
                    requestTimes = canContinue ? 1 : 0;
                    if (!canContinue) {
                        someoneRegister = false;
                    }
                }
                return canContinue;
            }
        }

        public void unregister() {
            lock (locker) {
                if (someoneRegister) {
                    requestTimes = requestTimes > 0 ? 1 : 0;
                    someoneRegister = false;
                }
            }
        }

        public void release() {
            lock (locker) {
                requestTimes = 0;
                someoneRegister = false;
            }
        }
    }
}
