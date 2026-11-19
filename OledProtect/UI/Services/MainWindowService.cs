using Microsoft.Extensions.Options;
using OledProtect.Infrastructure.Hosting;
using OledProtect.UI.Services.Interfaces;
using OledProtect.UI.ViewModels;
using System.ComponentModel;
using System.Windows;

namespace OledProtect.UI.Services
{
    /// <summary>
    /// The settings window is created once and kept for the whole session. The close button parks it
    /// in the tray; only an exiting app lets the close go through.
    /// </summary>
    public sealed class MainWindowService : IMainWindowService
    {
        private readonly MainWindow _shell;
        private readonly MainViewModel _model;
        private readonly bool _beginInTray;
        private readonly IMainWindowAccessor _accessor;
        private readonly IUnsavedSettingsService _editGuard;

        // Flipped once, when the app starts quitting; afterwards closes are no longer intercepted.
        private bool _appIsQuitting;

        public MainWindowService(
            MainWindow window,
            MainViewModel viewModel,
            IOptions<ApplicationOptions> appOptions,
            IMainWindowAccessor windowAccessor,
            IUnsavedSettingsService unsavedSettings)
        {
            _shell = window;
            _model = viewModel;
            _beginInTray = appOptions.Value.StartHidden;
            _accessor = windowAccessor;
            _editGuard = unsavedSettings;
        }

        public void SetupMainWindow()
        {
            _shell.Closing += ParkInsteadOfClosing;
            _shell.DataContext = _model;
            _accessor.SetMainWindow(_shell);

            Action firstAppearance = _beginInTray ? _shell.Hide : ShowMainWindow;
            firstAppearance();
        }

        public void ShowMainWindow()
        {
            // Restore first, so the window never flashes up as a taskbar-only minimized entry.
            if (_shell.WindowState is WindowState.Minimized)
                _shell.WindowState = WindowState.Normal;

            _shell.Show();
            _shell.Activate();
        }

        public void PrepareForShutdown() => _appIsQuitting = true;

        private void ParkInsteadOfClosing(object? sender, CancelEventArgs e)
        {
            if (!_appIsQuitting)
            {
                e.Cancel = true;
                bool mayLeave = _editGuard.ConfirmHide();
                if (mayLeave)
                    _shell.Hide();
            }
        }
    }
}
