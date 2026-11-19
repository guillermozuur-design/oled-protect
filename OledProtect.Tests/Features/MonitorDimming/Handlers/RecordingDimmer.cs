using OledProtect.Features.MonitorDimming.Services.Interfaces;

namespace OledProtect.Tests.Features.MonitorDimming.Handlers
{
    /// <summary>
    /// Writes each call down as a line such as "dim GSM-1 15" or "undim GSM-1". Calls for a monitor listed in
    /// <see cref="Broken"/> fail with an <see cref="InvalidOperationException"/> after being written down.
    /// </summary>
    internal sealed class RecordingDimmer : IMonitorDimmingService
    {
        public List<string> Calls { get; } = [];

        public HashSet<string> Broken { get; } = [];

        public Dictionary<string, uint> LeftDimmed { get; } = [];

        public Task DimMonitorAsync(string? hardwareId, int percent) => Note($"dim {hardwareId} {percent}", hardwareId);

        public Task UndimMonitorAsync(string hardwareId) => Note($"undim {hardwareId}", hardwareId);

        public Task<bool> SetScheduledBrightnessAsync(string hardwareId, int percent) => throw new NotSupportedException();

        public Dictionary<string, uint> GetDimmedMonitors() => new(LeftDimmed);

        private Task Note(string call, string? hardwareId)
        {
            Calls.Add(call);
            return hardwareId is not null && Broken.Contains(hardwareId)
                ? Task.FromException(new InvalidOperationException($"DDC/CI failure on {hardwareId}"))
                : Task.CompletedTask;
        }
    }
}
