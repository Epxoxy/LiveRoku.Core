namespace LiveRoku.Core.Common {
    using System;
    internal class CompleteFirstInvoker {
        //public
        public int RequestTimes { get; private set; }
        public bool IsInvoking { get; private set; }
        public bool ResetTimesOnInvoking { get; set; }
        //private
        private Action action;
        private object locker = new object();

        public CompleteFirstInvoker(Action action) {
            this.action = action;
        }
        public void invoke() {
            invoke(false);
        }

        public void invoke(bool internalCall) {
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

        public void revoke() {
            lock (locker) {
                RequestTimes = 0;
            }
        }
    }
}
