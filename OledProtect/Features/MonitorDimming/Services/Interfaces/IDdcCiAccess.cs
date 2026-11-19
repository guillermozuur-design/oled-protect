namespace OledProtect.Features.MonitorDimming.Services.Interfaces
{
    /// <summary>
    /// Entry point to the DDC/CI bus; the seam that lets the dimming logic run against fakes in tests.
    /// </summary>
    public interface IDdcCiAccess
    {
        /// <summary>
        /// Opens a channel to the panel shown as <paramref name="gdiDeviceName"/> (<c>\\.\DISPLAYn</c>).
        /// Null when that panel cannot be reached; otherwise the caller owns and disposes the channel.
        /// </summary>
        IDdcCiSession? OpenSession(string gdiDeviceName);
    }
}
