namespace OledProtect.Features.MonitorInformation.Models
{
    /// <summary>
    /// Outcome of probing one monitor over DDC/CI.
    /// </summary>
    /// <param name="IsSupported">True when the panel answered a brightness read or handed out a capabilities string.</param>
    /// <param name="MaxBrightness">Top of the panel's brightness range; 0 when the panel never reported one.</param>
    public readonly record struct DdcCiCapabilities(
        bool IsSupported,
        uint MaxBrightness);
}
