using OledProtect.Features.MonitorInformation.Models;

namespace OledProtect.Features.MonitorInformation.Services.Interfaces
{
    /// <summary>
    /// The app's view of the attached monitors: a cached, DDC/CI-probed list plus quick unprobed readings.
    /// </summary>
    public interface IMonitorInfoManager
    {
        /// <summary>
        /// The probed monitor list. The first call scans; later calls get the cached scan. Callers that arrive
        /// while the scan runs await the same one.
        /// </summary>
        Task<IReadOnlyList<MonitorInfo>> GetCurrentMonitorsAsync();

        /// <summary>
        /// Scans again and makes the result the new cached list.
        /// </summary>
        Task<IReadOnlyList<MonitorInfo>> RefreshMonitorsAsync();

        /// <summary>
        /// A fresh, unprobed reading straight from Windows. Monitors without a resolved hardware ID are included.
        /// </summary>
        List<MonitorInfo> GetLatestMonitorsBasicInfo();

        /// <summary>
        /// Probes each monitor in <paramref name="readings"/> over DDC/CI, in place. Monitors without a hardware ID
        /// cannot be managed and are taken out of the list.
        /// </summary>
        void EnrichMonitorInfoList(List<MonitorInfo> readings);
    }
}
