namespace OledProtect.Features.MonitorDimming.Services.Interfaces
{
    /// <summary>
    /// An open DDC/CI channel to one panel. All reads and writes go through the same physical monitor handle,
    /// which disposing the channel releases.
    /// </summary>
    public interface IDdcCiSession : IDisposable
    {
        /// <summary>The panel's current brightness in its own units, or null when it did not answer.</summary>
        uint? GetBrightness();

        /// <summary>Writes a brightness in the panel's own units (not a percentage). False when it was refused.</summary>
        bool SetBrightness(uint rawValue);
    }
}
