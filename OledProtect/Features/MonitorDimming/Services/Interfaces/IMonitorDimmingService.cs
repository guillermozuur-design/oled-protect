namespace OledProtect.Features.MonitorDimming.Services.Interfaces
{
    /// <summary>
    /// Changes monitors' hardware brightness over DDC/CI: dimming, undimming and the brightness schedule.
    /// </summary>
    public interface IMonitorDimmingService
    {
        /// <summary>
        /// Remembers the monitor's current brightness and lowers it to <paramref name="percent"/> (0-100, mapped
        /// onto the panel's own range).
        /// </summary>
        Task DimMonitorAsync(string? hardwareId, int percent);

        /// <summary>
        /// Puts the monitor back on the brightness remembered when it was dimmed. Does nothing when it is not dimmed.
        /// </summary>
        Task UndimMonitorAsync(string hardwareId);

        /// <summary>
        /// Applies a brightness (percent, 0-100) chosen by the brightness schedule.
        /// </summary>
        /// <returns>False when the monitor did not answer, so the caller can try again later.</returns>
        Task<bool> SetScheduledBrightnessAsync(string hardwareId, int percent);

        /// <summary>
        /// The monitors that are dimmed right now, with the raw brightness each will be restored to.
        /// </summary>
        Dictionary<string, uint> GetDimmedMonitors();
    }
}
