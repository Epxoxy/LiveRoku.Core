namespace LiveRoku.Core.Common.Helpers {
    //Await task until unregister manually
    //Always keep the newest request and igore all the early request
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
                requestTimes = 0;
                return true;
            }
        }

        public bool unregisterAndReRegister() {
            lock (locker) {
                bool registrable = false;
                //Only work if current is registered
                if (someoneRegister) {
                    //If latest request not exist
                    //We can't continue register
                    registrable = requestTimes > 0;
                    if (!registrable) {
                        someoneRegister = false;
                    } else {
                        requestTimes = 0;
                    }
                }
                return registrable;
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
