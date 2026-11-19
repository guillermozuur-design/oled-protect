namespace OLED_Sleeper.Features.MonitorDimming.Services.Interfaces
{
    /// <summary>
    /// Persists the pre-dim brightness of dimmed monitors, so a crash or shutdown while dimmed can be undone
    /// on the next start.
    /// </summary>
    public interface IMonitorBrightnessStateService
    {
        /// <summary>Hardware ID to raw pre-dim brightness, as last saved. Empty when nothing could be read.</summary>
        Dictionary<string, uint> LoadState();

        /// <summary>Replaces what is saved with <paramref name="snapshot"/>.</summary>
        void SaveState(Dictionary<string, uint> snapshot);
    }
}
