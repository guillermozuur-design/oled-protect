namespace OLED_Sleeper.UI.Services.Interfaces
{
    // Owns the lifetime of the settings window: first appearance, bringing it back, and close-to-tray.
    public interface IMainWindowService
    {
        // Registers the window, binds the view model and shows or hides it according to StartHidden.
        void SetupMainWindow();

        // Shows, un-minimizes and focuses the window.
        void ShowMainWindow();

        // From now on a close really closes; used while the app is exiting.
        void PrepareForShutdown();
    }
}
