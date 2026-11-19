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
    // Owns the app's lifetime so App.xaml.cs only has to forward WPF events. Startup runs in a strict order:
    // storage (logging writes there) -> logging -> single-instance check -> DI -> services -> window/tray.
    public sealed class ApplicationBootstrapper(string[] args) : IDisposable
    {
        private const string DialogTitle = "OLED Protect";

        // Upper bound on how long exit waits for monitors to return to normal brightness. If it runs out the
        // saved brightness state stays on disk and the next start finishes the job.
        private static readonly TimeSpan RestoreBudget = TimeSpan.FromSeconds(10);

        private readonly ApplicationOptions _options = CommandLineHelper.ParseArguments(args);

        // Built by hand instead of resolved: both are needed before the container exists and after it is gone.
        private readonly IApplicationShutdown _shutdown = new ApplicationShutdown();
        private readonly IDispatcher _uiThread = new ApplicationDispatcher();

        private IServiceProvider? _container;
        private IApplicationInstanceManager? _singleInstance;
        private IApplicationOrchestrator? _monitorServices;
        private IMainWindowService? _window;
        private ITrayIconService? _tray;
        private IUnsavedSettingsService? _unsaved;
        private bool _shuttingDown;

        public void Initialize()
        {
            var storage = PrepareStorageOrQuit();
            if (storage is null)
                return;

            LoggingConfigurator.Configure(storage.DirectoryPath);

            _singleInstance = new ApplicationInstanceManager(_uiThread, _shutdown);
            _singleInstance.Initialize();
            if (!_singleInstance.IsFirstInstance)
            {
                Log.Information("Already running elsewhere; asked that copy to show itself and quitting.");
                return;
            }

            _container = ServiceConfigurator.ConfigureServices(_options, storage);
            StartServices(_container);
        }

        private void StartServices(IServiceProvider services)
        {
            _monitorServices = services.GetRequiredService<IApplicationOrchestrator>();
            _monitorServices.Start();

            _window = services.GetRequiredService<IMainWindowService>();
            _window.SetupMainWindow();

            _unsaved = services.GetRequiredService<IUnsavedSettingsService>();
            _tray = services.GetRequiredService<ITrayIconService>();
            _tray.Initialize(ShowWindow, OnTrayExit);

            // No-op unless taskbar dimming is switched on in the Algemeen tab.
            services.GetRequiredService<TaskbarDimmer>().Start();

            _singleInstance!.SetShowMainWindowAction(ShowWindow);
        }

        private void ShowWindow() => _window?.ShowMainWindow();

        // Returns null after telling the user what is wrong; the app is already shutting down then.
        private IStorageRoot? PrepareStorageOrQuit()
        {
            var disk = new FileSystem();
            var root = new StorageRoot(disk, BuildMode.IsPortable);

            if (new StorageRootPreparer(disk, root).TryPrepare(out string? problem))
                return root;

            string hint = BuildMode.IsPortable
                ? "Zet de map van OLED Protect op een plek waar je mag schrijven (bijvoorbeeld je Bureaublad) en start opnieuw."
                : "Controleer of je gebruikersprofiel bereikbaar is en start OLED Protect opnieuw.";
            string nl = Environment.NewLine;

            new MessageBoxDialogService(new MainWindowAccessor()).ShowError(problem + nl + nl + hint, DialogTitle);
            _shutdown.Shutdown();
            return null;
        }

        // Deferred to after pending input so the click on "Exit" cannot also answer the unsaved-changes prompt.
        private void OnTrayExit() => _ = _uiThread.InvokeAfterInputAsync(ExitUnlessCancelled);

        // Only the tray route asks about unsaved edits; Windows logoff and OnExit go straight to ShutdownApp.
        private void ExitUnlessCancelled()
        {
            if (_unsaved is not null && !_unsaved.ConfirmExit())
            {
                Log.Information("User kept the app open because of unsaved settings.");
                return;
            }

            ShutdownApp();
        }

        // Safe to call more than once (tray exit, session end and OnExit can all arrive).
        public void ShutdownApp()
        {
            if (_shuttingDown)
                return;
            _shuttingDown = true;

            _window?.PrepareForShutdown();
            RestoreMonitorsBeforeExit();
            _tray?.Dispose();
            _singleInstance?.Dispose();

            Log.Information("=== OLED Protect stopped ===");
            Log.CloseAndFlush();

            _shutdown.Shutdown();
        }

        // Runs on the thread pool because during OnExit the UI dispatcher no longer pumps, so awaiting
        // here directly could deadlock. A second instance never started an orchestrator.
        private void RestoreMonitorsBeforeExit()
        {
            if (_monitorServices is not { } orchestrator)
                return;

            Log.Information("Exiting: putting all monitors back to normal.");
            try
            {
                bool finished = Task.Run(orchestrator.StopAsync).Wait(RestoreBudget);
                if (!finished)
                    Log.Warning("Monitor restore still busy after {Budget}; exiting regardless.", RestoreBudget);
            }
            catch (Exception failure)
            {
                Log.Error(failure, "Stopping the monitor services threw during exit.");
            }
        }

        public void Dispose()
        {
            _tray?.Dispose();
            _singleInstance?.Dispose();
            (_container as IDisposable)?.Dispose();
        }
    }
}
