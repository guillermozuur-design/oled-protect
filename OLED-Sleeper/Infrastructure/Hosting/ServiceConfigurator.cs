using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OLED_Sleeper.Features.BrightnessSchedule.Services;
using OLED_Sleeper.Features.Flights;
using OLED_Sleeper.Features.KeepAwakeApps;
using OLED_Sleeper.Features.MonitorBehavior.Commands;
using OLED_Sleeper.Features.MonitorBehavior.Handlers;
using OLED_Sleeper.Features.MonitorBlackout.Commands;
using OLED_Sleeper.Features.MonitorBlackout.Handlers;
using OLED_Sleeper.Features.MonitorBlackout.Services;
using OLED_Sleeper.Features.MonitorBlackout.Services.Interfaces;
using OLED_Sleeper.Features.MonitorDimming.Commands;
using OLED_Sleeper.Features.MonitorDimming.Handlers;
using OLED_Sleeper.Features.MonitorDimming.Services;
using OLED_Sleeper.Features.MonitorDimming.Services.Interfaces;
using OLED_Sleeper.Features.MonitorIdleDetection.Services;
using OLED_Sleeper.Features.MonitorIdleDetection.Services.Interfaces;
using OLED_Sleeper.Features.MonitorInformation.Services;
using OLED_Sleeper.Features.MonitorInformation.Services.Interfaces;
using OLED_Sleeper.Features.MonitorState.Commands;
using OLED_Sleeper.Features.MonitorState.Handlers;
using OLED_Sleeper.Features.MonitorState.Services;
using OLED_Sleeper.Features.MonitorState.Services.Interfaces;
using OLED_Sleeper.Features.Sky;
using OLED_Sleeper.Features.TaskbarDim;
using OLED_Sleeper.Features.UserSettings.Commands;
using OLED_Sleeper.Features.UserSettings.Handlers;
using OLED_Sleeper.Features.UserSettings.Services;
using OLED_Sleeper.Features.UserSettings.Services.Interfaces;
using OLED_Sleeper.Features.WorkMode.Services;
using OLED_Sleeper.Infrastructure.Hosting.Interfaces;
using OLED_Sleeper.Infrastructure.Runtime;
using OLED_Sleeper.Infrastructure.Runtime.Interfaces;
using OLED_Sleeper.Messaging;
using OLED_Sleeper.Messaging.Interfaces;
using OLED_Sleeper.Storage;
using OLED_Sleeper.Storage.Interfaces;
using OLED_Sleeper.UI.Services;
using OLED_Sleeper.UI.Services.Interfaces;
using OLED_Sleeper.UI.ViewModels;

namespace OLED_Sleeper.Infrastructure.Hosting
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
