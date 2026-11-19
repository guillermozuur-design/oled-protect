using Microsoft.Extensions.DependencyInjection;
using OledProtect.Features.TaskbarDim;
using OledProtect.Infrastructure.Helpers;
using OledProtect.Infrastructure.Hosting.Interfaces;
using OledProtect.Infrastructure.Runtime;
using OledProtect.Infrastructure.Runtime.Interfaces;
using OledProtect.Storage;
using OledProtect.Storage.Interfaces;
using OledProtect.UI.Services;
using OledProtect.UI.Services.Interfaces;
using Serilog;

namespace OledProtect.Infrastructure.Hosting
{
    // The whole lifetime of the tray app in one place; App.xaml.cs just calls in. Starting is a table of
    // stages run top to bottom, each of which may end the start-up (bad storage folder, second copy).
    public sealed class ApplicationBootstrapper(string[] args) : IDisposable
    {
        private const string AppTitle = "OLED Protect";

        // How long quitting may block on bringing the screens back. Whatever is left over is picked up
        // from the stored brightness state on the next launch.
        private static readonly TimeSpan ScreenRestoreLimit = TimeSpan.FromSeconds(10);

        private static readonly string ParagraphBreak = Environment.NewLine + Environment.NewLine;

        private readonly ApplicationOptions _options = CommandLineHelper.ParseArguments(args);

        // Not taken from DI: these must work before the container is built and after it is disposed.
        private readonly IApplicationShutdown _process = new ApplicationShutdown();
        private readonly IDispatcher _ui = new ApplicationDispatcher();

        private IStorageRoot? _root;
        private IApplicationInstanceManager? _gate;
        private IServiceProvider? _services;
        private IApplicationOrchestrator? _engine;
        private IMainWindowService? _mainWindow;
        private IUnsavedSettingsService? _pendingEdits;
        private ITrayIconService? _trayIcon;
        private int _stopping;

        public void Initialize()
        {
            Func<bool>[] stages =
            [
                OpenStorageFolder,
                Continue(() => LoggingConfigurator.Configure(_root!.DirectoryPath)),
                ClaimSingleInstance,
                Continue(() => _services = ServiceConfigurator.ConfigureServices(_options, _root!)),
                Continue(() => (_engine = Resolve<IApplicationOrchestrator>()).Start()),
                Continue(() => (_mainWindow = Resolve<IMainWindowService>()).SetupMainWindow()),
                Continue(AttachTrayIcon),
                Continue(() => Resolve<TaskbarDimmer>().Start()), // does nothing while the option is off
                Continue(() => _gate!.SetShowMainWindowAction(BringUpMainWindow)),
            ];

            foreach (var stage in stages)
            {
                if (!stage())
                    break;
            }
        }

        // Safe to call more than once: tray exit, session end and OnExit can all arrive.
        public void ShutdownApp()
        {
            if (Interlocked.Exchange(ref _stopping, 1) == 1)
                return;

            _mainWindow?.PrepareForShutdown();
            BringScreensBack();
            ReleaseShell();

            Log.Information("OLED Protect has exited.");
            Log.CloseAndFlush();
            _process.Shutdown();
        }

        public void Dispose()
        {
            ReleaseShell();
            (_services as IDisposable)?.Dispose();
        }

        private static Func<bool> Continue(Action stage) => () =>
        {
            stage();
            return true;
        };

        private T Resolve<T>() where T : notnull => _services!.GetRequiredService<T>();

        // Runs before logging exists (the log file lives in this folder), so trouble goes straight to a dialog.
        private bool OpenStorageFolder()
        {
            var disk = new FileSystem();
            var root = new StorageRoot(disk, BuildMode.IsPortable);

            if (!new StorageRootPreparer(disk, root).TryPrepare(out string? reason))
            {
                string advice = BuildMode.IsPortable
                    ? "Verplaats de map van OLED Protect naar een plek met schrijfrechten, zoals Documenten, en open het programma daarna opnieuw."
                    : "Kijk of je Windows-profiel (de map AppData) beschikbaar is en open OLED Protect daarna opnieuw.";

                new MessageBoxDialogService(new MainWindowAccessor()).ShowError(reason + ParagraphBreak + advice, AppTitle);
                _process.Shutdown();
                return false;
            }

            _root = root;
            return true;
        }

        private bool ClaimSingleInstance()
        {
            _gate = new ApplicationInstanceManager(_ui, _process);
            _gate.Initialize();

            if (_gate.IsFirstInstance)
                return true;

            Log.Information("Second copy started: the running one was asked to come forward, this one stops here.");
            return false;
        }

        private void AttachTrayIcon()
        {
            _pendingEdits = Resolve<IUnsavedSettingsService>();
            _trayIcon = Resolve<ITrayIconService>();
            _trayIcon.Initialize(BringUpMainWindow, () => _ = _ui.InvokeAfterInputAsync(QuitFromTray));
        }

        private void BringUpMainWindow() => _mainWindow?.ShowMainWindow();

        // Queued behind pending input (see AttachTrayIcon) so the menu click cannot also answer the prompt.
        // Only this route asks about unsaved edits; logoff and OnExit quit without asking.
        private void QuitFromTray()
        {
            bool mayQuit = _pendingEdits?.ConfirmExit() ?? true;
            if (mayQuit)
                ShutdownApp();
            else
                Log.Information("Quit from tray cancelled; there are unsaved edits.");
        }

        // The restore is pushed onto the thread pool: by the time OnExit runs, the UI dispatcher no longer
        // pumps messages, so waiting for it on this thread could hang. A second copy never had an engine.
        private void BringScreensBack()
        {
            if (_engine is null)
                return;

            Log.Information("Quitting; returning every screen to its normal state.");
            Task restore = Task.Run(_engine.StopAsync);
            Task winner = Task.WhenAny(restore, Task.Delay(ScreenRestoreLimit)).GetAwaiter().GetResult();

            if (winner != restore)
                Log.Warning("Screens not fully restored within {Limit}; quitting anyway.", ScreenRestoreLimit);
            else if (restore.Exception is { } trouble)
                Log.Error(trouble.GetBaseException(), "Restoring the screens failed while quitting.");
            else if (restore.IsCanceled)
                Log.Warning("Restoring the screens was cancelled while quitting.");
        }

        private void ReleaseShell()
        {
            _trayIcon?.Dispose();
            _gate?.Dispose();
        }
    }
}
