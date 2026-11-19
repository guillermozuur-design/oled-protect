namespace OLED_Sleeper.UI.Services.Interfaces
{
    // The pending edits of the monitor settings. UI thread only.
    public interface IUnsavedSettings
    {
        // True while at least one monitor differs from what is on disk.
        bool IsDirty { get; }

        // Validates and writes; false means validation refused and the file is untouched.
        bool TrySaveChanges();

        // Reverts every monitor to its last saved values.
        void DiscardChanges();
    }
}
