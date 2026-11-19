namespace OledProtect.Features.MonitorState.Services.Interfaces
{
    // Watches for screens being plugged in, unplugged or rearranged and sends a
    // SynchronizeMonitorStateCommand when that happens.
    public interface IMonitorStateWatcher : IDisposable
    {
        // Reads the current screens, syncs once, then keeps watching. A second call while running is a no-op.
        void Start();

        // After this returns no further synchronisation is sent; a check already running finishes quietly.
        void Stop();
    }
}
