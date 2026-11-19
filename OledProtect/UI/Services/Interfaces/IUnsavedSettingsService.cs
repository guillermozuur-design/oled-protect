namespace OledProtect.UI.Services.Interfaces
{
    // Guards actions that would throw away pending edits. Shows a modal prompt, so UI thread only.
    public interface IUnsavedSettingsService
    {
        // Before the settings window goes to the tray. True = go on.
        bool ConfirmHide();

        // Before the application quits. True = go on.
        bool ConfirmExit();
    }
}
