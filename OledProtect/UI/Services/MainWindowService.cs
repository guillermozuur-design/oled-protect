using Microsoft.Extensions.Options;
using OledProtect.Infrastructure.Hosting;
using OledProtect.UI.Services.Interfaces;
using OledProtect.UI.ViewModels;
using System.ComponentModel;
using System.Windows;

namespace OledProtect.UI.Services
{
    // The settings window lives as long as the app: closing it only sends it back to the tray.
    // A real close happens only once the app is shutting down.
    public class MainWindowService(
        MainWindow window,
        MainViewModel viewModel,
        IOptions<ApplicationOptions> appOptions,
        IMainWindowAccessor windowRegistry,
        IUnsavedSettingsService pendingEditsGuard) : IMainWindowService
    {
        private bool _exiting;

        public void SetupMainWindow()
        {
            windowRegistry.SetMainWindow(window);
            window.DataContext = viewModel;
            window.Closing += InterceptClose;

            // Started from Windows' autostart the app normally goes straight to the tray.
            if (!appOptions.Value.StartHidden)
                ShowMainWindow();
            else
                window.Hide();
        }

        public void ShowMainWindow()
        {
            window.Show();
            if (window.WindowState is WindowState.Minimized)
                window.WindowState = WindowState.Normal;
            window.Activate();
        }

        public void PrepareForShutdown() => _exiting = true;

        private void InterceptClose(object? sender, CancelEventArgs args)
        {
            if (_exiting)
                return;

            // Never close; hide instead, but only after pending edits have been saved or dropped.
            args.Cancel = true;
            if (pendingEditsGuard.ConfirmHide())
                window.Hide();
        }
    }
}
