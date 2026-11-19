using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OledProtect.Features.BrightnessSchedule.Services;
using OledProtect.Features.Flights;
using OledProtect.Features.KeepAwakeApps;
using OledProtect.Features.MonitorBehavior.Commands;
using OledProtect.Features.MonitorBehavior.Handlers;
using OledProtect.Features.MonitorBlackout.Commands;
using OledProtect.Features.MonitorBlackout.Handlers;
using OledProtect.Features.MonitorBlackout.Services;
using OledProtect.Features.MonitorBlackout.Services.Interfaces;
using OledProtect.Features.MonitorDimming.Commands;
using OledProtect.Features.MonitorDimming.Handlers;
using OledProtect.Features.MonitorDimming.Services;
using OledProtect.Features.MonitorDimming.Services.Interfaces;
using OledProtect.Features.MonitorIdleDetection.Services;
using OledProtect.Features.MonitorIdleDetection.Services.Interfaces;
using OledProtect.Features.MonitorInformation.Services;
using OledProtect.Features.MonitorInformation.Services.Interfaces;
using OledProtect.Features.MonitorState.Commands;
using OledProtect.Features.MonitorState.Handlers;
using OledProtect.Features.MonitorState.Services;
using OledProtect.Features.MonitorState.Services.Interfaces;
using OledProtect.Features.Sky;
using OledProtect.Features.TaskbarDim;
using OledProtect.Features.UserSettings.Commands;
using OledProtect.Features.UserSettings.Handlers;
using OledProtect.Features.UserSettings.Services;
using OledProtect.Features.UserSettings.Services.Interfaces;
using OledProtect.Features.WorkMode.Services;
using OledProtect.Infrastructure.Hosting.Interfaces;
using OledProtect.Infrastructure.Runtime;
using OledProtect.Infrastructure.Runtime.Interfaces;
using OledProtect.Messaging;
using OledProtect.Messaging.Interfaces;
using OledProtect.Storage;
using OledProtect.Storage.Interfaces;
using OledProtect.UI.Services;
using OledProtect.UI.Services.Interfaces;
using OledProtect.UI.ViewModels;

namespace OledProtect.Infrastructure.Hosting
{
    // The composition root. Everything is a singleton except command handlers, which are cheap and stateless.
    public static class ServiceConfigurator
    {
        public static IServiceProvider ConfigureServices(ApplicationOptions applicationOptions, IStorageRoot storageRoot) =>
            new ServiceCollection()
                .AddPlumbing(applicationOptions, storageRoot)
                .AddCommandHandlers()
                .AddMonitorServices()
                .AddFeatureSettings()
                .AddUserInterface()
                .BuildServiceProvider();

        // Cross-cutting pieces: messaging, threading, options and on-disk storage.
        private static IServiceCollection AddPlumbing(this IServiceCollection di, ApplicationOptions options, IStorageRoot root) => di
            .AddSingleton<IMediator, Mediator>()
            .AddSingleton<IDispatcher, ApplicationDispatcher>()
            .AddSingleton(Options.Create(options))
            .AddSingleton(root)
            .AddSingleton<IFileSystem, FileSystem>()
            .AddSingleton<IAppDataFileStore, AppDataFileStore>()
            .AddSingleton<IMonitorSettingsFileService, MonitorSettingsFileService>();

        private static IServiceCollection AddCommandHandlers(this IServiceCollection di) => di
            .AddHandler<ApplyMonitorActiveBehaviorCommand, ApplyMonitorActiveBehaviorCommandHandler>()
            .AddHandler<ApplyMonitorIdleBehaviorCommand, ApplyMonitorIdleBehaviorCommandHandler>()
            .AddHandler<ApplyBlackoutOverlayCommand, ApplyBlackoutOverlayCommandHandler>()
            .AddHandler<HideBlackoutOverlayCommand, HideBlackoutOverlayCommandHandler>()
            .AddHandler<ApplyDimCommand, ApplyDimCommandHandler>()
            .AddHandler<ApplyUndimCommand, ApplyUndimCommandHandler>()
            .AddHandler<RestoreBrightnessOnAllMonitorsCommand, RestoreBrightnessOnAllMonitorsCommandHandler>()
            .AddHandler<SynchronizeMonitorStateCommand, SynchronizeMonitorStateCommandHandler>()
            .AddHandler<RestoreMonitorStateCommand, RestoreMonitorStateCommandHandler>()
            .AddHandler<ApplySettingsChangeCommand, ApplySettingsChangeCommandHandler>();

        private static IServiceCollection AddHandler<TCommand, THandler>(this IServiceCollection di)
            where TCommand : ICommand
            where THandler : class, ICommandHandler<TCommand> =>
            di.AddTransient<ICommandHandler<TCommand>, THandler>();

        // Detection, dimming, blackout, brightness and the orchestrator that ties them together.
        private static IServiceCollection AddMonitorServices(this IServiceCollection di) => di
            .AddSingleton<IMonitorInfoManager, MonitorInfoManager>()
            .AddSingleton<IMonitorInfoProvider, MonitorInfoProvider>()
            .AddSingleton<IMonitorLayoutService, MonitorLayoutService>()
            .AddSingleton<IWorkspaceService, WorkspaceService>()
            .AddSingleton<IMonitorStateWatcher, MonitorStateWatcher>()
            .AddSingleton<IDdcCiAccess, DdcCiAccess>()
            .AddSingleton(BrightnessFadeOptions.Default)
            .AddSingleton<IOriginalBrightnessStore, OriginalBrightnessStore>()
            .AddSingleton<IMonitorBrightnessStateService, MonitorBrightnessStateService>()
            .AddSingleton<IMonitorDimmingService, MonitorDimmingService>()
            .AddSingleton<ManualBrightnessService>()
            .AddSingleton<IBrightnessScheduleService, BrightnessScheduleService>()
            .AddSingleton<IOverlayWindowFactory, OverlayWindowFactory>()
            .AddSingleton<IMonitorBlackoutService, MonitorBlackoutService>()
            .AddSingleton<IMonitorIdleDetectionService, MonitorIdleDetectionService>()
            .AddSingleton<IWorkModeService, WorkModeService>()
            .AddSingleton<GlobalHotkeyService>()
            .AddSingleton<TaskbarDimmer>()
            .AddSingleton<ScreensaverPreview>()
            .AddSingleton<IApplicationOrchestrator, ApplicationOrchestrator>();

        // Small per-feature settings objects, each persisted in its own JSON file.
        private static IServiceCollection AddFeatureSettings(this IServiceCollection di) => di
            .AddSingleton<GlobalIdleSettings>()
            .AddSingleton<GlobalBrightnessSchedule>()
            .AddSingleton<KeepAwakeAppsSettings>()
            .AddSingleton<TaskbarDimSettings>()
            .AddSingleton<PanoramaSettings>()
            .AddSingleton<FlightsSettings>();

        private static IServiceCollection AddUserInterface(this IServiceCollection di) => di
            .AddSingleton<IMainWindowAccessor, MainWindowAccessor>()
            .AddSingleton<MainViewModel>()
            .AddSingleton<MainWindow>()
            .AddSingleton<IMainWindowService, MainWindowService>()
            .AddSingleton<ITrayIconService, TrayIconService>()
            .AddSingleton<IDialogService, MessageBoxDialogService>()
            // The view model is what knows about pending edits, so it also serves that role.
            .AddSingleton<IUnsavedSettings>(provider => provider.GetRequiredService<MainViewModel>())
            .AddSingleton<IUnsavedSettingsService, UnsavedSettingsService>()
            .AddSingleton<IMonitorSettingsSaveService, MonitorSettingsSaveService>();
    }
}
