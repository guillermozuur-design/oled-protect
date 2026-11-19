using OledProtect.Infrastructure.Hosting.Interfaces;
using OledProtect.Infrastructure.Runtime.Interfaces;

namespace OledProtect.Infrastructure.Hosting
{
    // Single-instance guard built from two named kernel objects: a mutex that marks "a copy is running"
    // and an auto-reset event that a later launch sets to ask the running copy to show itself.
    public sealed class ApplicationInstanceManager(IDispatcher dispatcher, IApplicationShutdown exit) : IApplicationInstanceManager
    {
        // Kernel object names shared between processes; left unchanged for now (rename planned separately).
        private const string LockName = "OledProtect-Mutex";
        private const string WakeEventName = "OledProtect-ShowWindow";

        private Mutex? _lock;
        private bool _holdsLock;
        private EventWaitHandle? _wakeEvent;
        private RegisteredWaitHandle? _wakeSubscription;
        private Action? _onWake;

        public bool IsFirstInstance => _holdsLock;

        public void Initialize()
        {
            _lock = new Mutex(initiallyOwned: true, LockName, out _holdsLock);

            if (_holdsLock)
                ListenForOtherLaunches();
            else
                HandOverToRunningCopy();
        }

        public void SetShowMainWindowAction(Action showMainWindowAction)
        {
            ArgumentNullException.ThrowIfNull(showMainWindowAction);
            Volatile.Write(ref _onWake, showMainWindowAction);
        }

        // The pool thread waits on the event, so no thread of our own is kept alive for this.
        private void ListenForOtherLaunches()
        {
            _wakeEvent = new EventWaitHandle(false, EventResetMode.AutoReset, WakeEventName);
            _wakeSubscription = ThreadPool.RegisterWaitForSingleObject(
                _wakeEvent, (_, _) => OnWakeRequested(), null, Timeout.Infinite, executeOnlyOnce: false);
        }

        private void OnWakeRequested()
        {
            var show = Volatile.Read(ref _onWake);
            if (show is null)
                return; // Poked before the window existed; nothing to bring forward yet.

            try
            {
                dispatcher.Invoke(show);
            }
            catch (Exception)
            {
                // The dispatcher is gone while shutting down. A throw here would kill the process from a pool thread.
            }
        }

        private void HandOverToRunningCopy()
        {
            // The running copy may still be starting and not have created its event yet; then we just leave.
            if (EventWaitHandle.TryOpenExisting(WakeEventName, out var wake))
            {
                using (wake)
                    wake.Set();
            }

            exit.Shutdown();
        }

        public void Dispose()
        {
            _wakeSubscription?.Unregister(null);
            _wakeSubscription = null;
            _wakeEvent?.Dispose();
            _wakeEvent = null;

            if (_lock is null)
                return;

            if (_holdsLock)
            {
                try
                {
                    _lock.ReleaseMutex();
                }
                catch (ApplicationException)
                {
                    // Disposed from a thread that does not own the mutex; Windows frees it at process exit anyway.
                }
            }

            _lock.Dispose();
            _lock = null;
        }
    }
}
