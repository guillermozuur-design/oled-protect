using OLED_Sleeper.Features.MonitorDimming.Commands;
using OLED_Sleeper.Features.MonitorDimming.Services.Interfaces;
using OLED_Sleeper.Messaging.Interfaces;
using Serilog;

namespace OLED_Sleeper.Features.MonitorDimming.Handlers
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
