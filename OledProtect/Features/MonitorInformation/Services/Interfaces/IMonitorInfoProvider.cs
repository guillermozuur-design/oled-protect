using OledProtect.Features.MonitorInformation.Models;

namespace OledProtect.Features.MonitorInformation.Services.Interfaces
{
    /// <summary>
    /// Reads monitors from Windows. Listing them is quick; probing one over DDC/CI talks to the panel and is slow,
    /// so the two are kept apart.
    /// </summary>
    public interface IMonitorInfoProvider
    {
        /// <summary>
        /// Lists every attached monitor with its name, geometry, DPI and identity, without touching DDC/CI.
        /// A monitor whose hardware ID could not be worked out is listed with an empty one.
        /// </summary>
        List<MonitorInfo> GetAllMonitorsBasicInfo();

        /// <summary>
        /// Asks the panel behind <paramref name="target"/> whether it speaks DDC/CI and what its brightness range is.
        /// </summary>
        DdcCiCapabilities GetDdcCiCapabilities(MonitorInfo target);
    }
}
