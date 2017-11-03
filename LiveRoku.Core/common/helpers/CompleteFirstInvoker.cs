namespace LiveRoku.Core.Common {
    using System;
    internal class LastContinuableInvoker {
        //public
        public int RequestTimes { get; private set; }
        public bool IsInvoking { get; private set; }
        public bool ResetTimesOnInvoking { get; set; }
        //private
        private Action action;
        private object locker = new object();

        public LastContinuableInvoker(Action action) {
            this.action = action;
        }
        public void invoke() {
            invoke(false);
        }

        private void invoke(bool internalCall) {
            lock (locker) {
                if(!internalCall)
                    RequestTimes++;
                if (IsInvoking)
                    return;
                if (ResetTimesOnInvoking)
                    RequestTimes = 0;
                else
                    RequestTimes--;
                IsInvoking = true;
            }
            action?.Invoke();
        }

        public void fireActionOk() {
            lock (locker) {
                IsInvoking = false;
                if (RequestTimes <= 0)
                    return;
                RequestTimes--;
            }
            invoke(true);
        }

        public void reset() {
            lock (locker) {
                RequestTimes = 0;
            }
        }
    }
}
