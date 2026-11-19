namespace OledProtect.Features.MonitorDimming.Services.Interfaces
{
    /// <summary>
    /// The brightness each dimmed monitor had before it was dimmed. Every change is written through to disk.
    /// </summary>
    public interface IOriginalBrightnessStore
    {
        /// <summary>Looks up the remembered brightness; <paramref name="rawValue"/> is 0 when there is none.</summary>
        bool TryGetOriginal(string hardwareId, out uint rawValue);

        /// <summary>
        /// Remembers <paramref name="rawValue"/> unless the monitor already has an entry: dimming an already
        /// dimmed monitor must not replace its real original with the dimmed level.
        /// </summary>
        void RecordOriginal(string hardwareId, uint rawValue);

        /// <summary>Sets the remembered brightness, replacing any existing entry.</summary>
        void ReplaceOriginal(string hardwareId, uint rawValue);

        /// <summary>Forgets the monitor's entry, if it has one.</summary>
        void RemoveOriginal(string hardwareId);

        /// <summary>A copy of all entries, hardware ID to raw brightness.</summary>
        Dictionary<string, uint> GetAll();
    }
}
