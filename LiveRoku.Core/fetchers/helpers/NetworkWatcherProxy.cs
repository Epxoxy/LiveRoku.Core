using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace LiveRoku.Core {

    public interface INetworkWatcher {
        bool IsEnabled { get; }
        bool IsAvailable { get; }
        void assumeAvailability (bool available);
        void attach (Action<bool> onNewNetworkAvailability);
        void detach ();
    }

    internal class NetworkWatcherProxy : INetworkWatcher {
        public bool IsEnabled { get; private set; }
        public bool IsAvailable { get; private set; }

        private Action<bool> onNewNetworkAvailability;

        public void assumeAvailability (bool available) {
            this.IsAvailable = available;
        }

        public void attach (Action<bool> onNewNetworkAvailability) {
            setWatchOrNot (true);
            this.onNewNetworkAvailability = onNewNetworkAvailability;
        }

        public void detach () {
            setWatchOrNot (false);
            this.onNewNetworkAvailability = null;
        }

        private void setWatchOrNot (bool watchIt) {
            IsEnabled = watchIt;
            NetworkChange.NetworkAvailabilityChanged -= proxyEvent;
            if (!watchIt) return;
            NetworkChange.NetworkAvailabilityChanged += proxyEvent;
        }

        private void proxyEvent (object sender, NetworkAvailabilityEventArgs e) {
            IsAvailable = e.IsAvailable;
            onNewNetworkAvailability?.Invoke (e.IsAvailable);
        }
    }
}