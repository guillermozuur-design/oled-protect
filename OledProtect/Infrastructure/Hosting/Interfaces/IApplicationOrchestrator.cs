namespace OledProtect.Infrastructure.Hosting.Interfaces
{
    // Starts and stops the background monitor machinery as one unit.
    public interface IApplicationOrchestrator
    {
        void Start();

        // Completes once every monitor is back at its normal brightness.
        Task StopAsync();
    }
}
