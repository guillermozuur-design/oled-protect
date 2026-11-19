using OledProtect.Features.MonitorDimming.Commands;
using OledProtect.Features.MonitorDimming.Services.Interfaces;
using OledProtect.Messaging.Interfaces;
using Serilog;

namespace OledProtect.Features.MonitorDimming.Handlers
{
    /// <summary>
    /// Undims every monitor that a previous run left dimmed. Exceptions propagate to the startup code that sent it.
    /// </summary>
    public sealed class RestoreBrightnessOnAllMonitorsCommandHandler(IMonitorDimmingService dimming) : ICommandHandler<RestoreBrightnessOnAllMonitorsCommand>
    {
        public async Task HandleAsync(RestoreBrightnessOnAllMonitorsCommand request)
        {
            Log.Information("Looking for monitors a previous run left dimmed.");

            // The dimming service owns the recordings (and the state file behind them), so ask it, not the file.
            var leftDimmed = dimming.GetDimmedMonitors().Keys.ToList();
            if (leftDimmed.Count == 0) return;

            Log.Warning("{Count} monitor(s) were left dimmed; restoring their brightness.", leftDimmed.Count);
            foreach (string hardwareId in leftDimmed)
            {
                await dimming.UndimMonitorAsync(hardwareId);
            }
        }
    }
}
