namespace OledProtect.UI.Services.Interfaces
{
    // The notification-area icon with its menu, quick panel and hotkey.
    public interface ITrayIconService : IDisposable
    {
        // Creates the icon. The callbacks open the settings window and quit the app.
        void Initialize(Action openSettings, Action quit);
    }
}
