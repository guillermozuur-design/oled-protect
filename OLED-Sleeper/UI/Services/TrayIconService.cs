using Hardcodet.Wpf.TaskbarNotification;
using OLED_Sleeper.Features.MonitorBlackout.Services;
using OLED_Sleeper.Features.MonitorInformation.Services.Interfaces;
using OLED_Sleeper.Features.WorkMode.Services;
using OLED_Sleeper.Native;
using System.Windows.Threading;
using OLED_Sleeper.Infrastructure.Helpers;
using OLED_Sleeper.UI.Services.Interfaces;
using Serilog;
using System.Windows;
using System.Windows.Controls;

namespace OLED_Sleeper.UI.Services
{
    /// <summary>
    /// Manages the tray icon, its context menu, and related events for the application.
    /// </summary>
    public class TrayIconService : ITrayIconService
    {
        private TaskbarIcon? _notifyIcon;
        private Action? _showMainWindowAction;
        private Action? _exitApplicationAction;
        private readonly IWorkModeService _workModeService;
        private readonly GlobalHotkeyService _hotkeyService;
        private MenuItem? _workModeMenuItem;
        private System.Drawing.Icon? _normalIcon;
        private System.Drawing.Icon? _pausedIcon;

        private readonly IMonitorInfoManager _monitorInfoManager;
        private OverlayWindow? _previewOverlay;
        private Views.QuickPanelWindow? _quickPanel;

        public TrayIconService(IWorkModeService workModeService, IMonitorInfoManager monitorInfoManager, GlobalHotkeyService hotkeyService)
        {
            _workModeService = workModeService;
            _monitorInfoManager = monitorInfoManager;
            _hotkeyService = hotkeyService;
        }

        /// <summary>
        /// Initializes and displays the tray icon and its context menu.
        /// Also wires up left-click and double-click to show the main window.
        /// </summary>
        /// <param name="showMainWindowAction">Action to show the main window.</param>
        /// <param name="exitApplicationAction">Action to exit the application.</param>
        public void Initialize(Action showMainWindowAction, Action exitApplicationAction)
        {
            _showMainWindowAction = showMainWindowAction ?? throw new ArgumentNullException(nameof(showMainWindowAction));
            _exitApplicationAction = exitApplicationAction ?? throw new ArgumentNullException(nameof(exitApplicationAction));

            _notifyIcon = new TaskbarIcon { ToolTipText = "OLED Sleeper" };

            WireTrayIconEvents();
            SetupContextMenu();
            SetTrayIcon();

            _hotkeyService.Pressed += OnHotkeyPressed;
            _hotkeyService.Changed += OnHotkeyChanged;
            _hotkeyService.Register();
            _workModeService.Changed += OnWorkModeChanged;
            UpdateWorkModeVisuals(_workModeService.IsEnabled, showBalloon: false);
        }

        private void OnHotkeyChanged() => UpdateWorkModeVisuals(_workModeService.IsEnabled, showBalloon: false);

        /// <summary>
        /// First press opens the quick panel on the monitor under the mouse; further presses while the
        /// modifier is still held move the highlight, like Alt+Tab.
        /// </summary>
        private void OnHotkeyPressed()
        {
            if (_quickPanel != null)
            {
                _quickPanel.Next();
                return;
            }

            NativeMethods.GetCursorPos(out var cursor);
            var monitors = _monitorInfoManager.GetLatestMonitorsBasicInfo();
            var monitor = monitors.FirstOrDefault(m => m.Bounds.Contains(new Point(cursor.X, cursor.Y))) ?? monitors.FirstOrDefault();
            if (monitor == null) return;

            var hotkey = _hotkeyService.HotkeyText;
            var modifier = hotkey.Contains('+') ? hotkey[..hotkey.LastIndexOf('+')] : hotkey;
            var key = hotkey[(hotkey.LastIndexOf('+') + 1)..];
            _quickPanel = new Views.QuickPanelWindow(_workModeService,
                () => _showMainWindowAction?.Invoke(),
                ShowScreensaverPreview,
                monitor.Bounds,
                _hotkeyService.HoldKey,
                $"Houd {modifier} vast \u00b7 tik {key} = volgende \u00b7 loslaten = kiezen \u00b7 Esc = annuleren");
            _quickPanel.Closed += (_, _) => _quickPanel = null;
            _quickPanel.Show();
        }

        private void OnWorkModeChanged(bool enabled)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;
            dispatcher.BeginInvoke(() => UpdateWorkModeVisuals(enabled, showBalloon: true));
        }

        private void UpdateWorkModeVisuals(bool enabled, bool showBalloon)
        {
            if (_notifyIcon == null) return;

            if (_workModeMenuItem != null) _workModeMenuItem.IsChecked = enabled;

            var hotkeyHint = _hotkeyService.IsRegistered
                ? _hotkeyService.HotkeyText
                : "sneltoets niet beschikbaar";

            var until = _workModeService.EnabledUntil;
            var untilText = until != null ? $" tot {until.Value.ToLocalTime():HH:mm}" : "";

            _notifyIcon.ToolTipText = enabled
                ? $"OLED Sleeper – WERKMODUS{untilText}: alles blijft aan ({hotkeyHint} = snelmenu)"
                : $"OLED Sleeper – bescherming actief ({hotkeyHint} = snelmenu)";

            var icon = enabled ? _pausedIcon : _normalIcon;
            if (icon != null) _notifyIcon.Icon = icon;

            if (showBalloon)
            {
                _notifyIcon.ShowBalloonTip(
                    enabled ? "Werkmodus aan" + untilText : "Werkmodus uit",
                    enabled ? "Alle schermen blijven aan." : "Inbrandbescherming is weer actief.",
                    BalloonIcon.Info);
            }
        }

        /// <summary>
        /// Wires up tray icon mouse events for left-click and double-click actions.
        /// </summary>
        private void WireTrayIconEvents()
        {
            if (_notifyIcon == null) return;

            _notifyIcon.TrayMouseDoubleClick += (_, _) => _showMainWindowAction?.Invoke();
            _notifyIcon.TrayLeftMouseUp += (_, _) => _showMainWindowAction?.Invoke();
        }

        /// <summary>
        /// Sets up the tray icon's context menu.
        /// </summary>
        private void SetupContextMenu()
        {
            if (_notifyIcon == null) return;

            var contextMenu = new ContextMenu();

            contextMenu.Items.Add(CreateWorkModeMenuItem());
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(CreatePreviewMenuItem());
            contextMenu.Items.Add(CreateShowSettingsMenuItem());
            contextMenu.Items.Add(CreateStartupMenuItem());
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(CreateExitMenuItem());

            _notifyIcon.ContextMenu = contextMenu;
        }

        /// <summary>
        /// Creates the menu item used to show the main application settings window.
        /// </summary>
        /// <returns>A configured MenuItem for showing settings.</returns>
        private MenuItem CreateWorkModeMenuItem()
        {
            _workModeMenuItem = new MenuItem
            {
                Header = "Werkmodus (alles aan)",
                IsCheckable = true,
                IsChecked = _workModeService.IsEnabled
            };
            _workModeMenuItem.Click += (_, _) => _workModeService.SetEnabled(_workModeMenuItem.IsChecked);
            return _workModeMenuItem;
        }

        private MenuItem CreatePreviewMenuItem()
        {
            var item = new MenuItem { Header = "Sterren-screensaver bekijken" };
            item.Click += (_, _) => ShowScreensaverPreview();
            return item;
        }

        /// <summary>
        /// Shows the starfield on the monitor under the mouse, using the real overlay window, until the
        /// mouse is moved. Handy to see what the screensaver looks like without waiting for the idle timer.
        /// </summary>
        private void ShowScreensaverPreview()
        {
            if (_previewOverlay != null) return;

            NativeMethods.GetCursorPos(out var start);
            var monitor = _monitorInfoManager.GetLatestMonitorsBasicInfo()
                .FirstOrDefault(m => m.Bounds.Contains(new Point(start.X, start.Y)));
            if (monitor == null) return;

            _previewOverlay = new OverlayWindow(screensaver: true);
            _previewOverlay.Show(monitor.Bounds);

            var startedAt = DateTime.UtcNow;
            var poll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            poll.Tick += (_, _) =>
            {
                // Ignore the first second: the click that opened the menu still moves the mouse a bit.
                if ((DateTime.UtcNow - startedAt).TotalSeconds < 1)
                {
                    NativeMethods.GetCursorPos(out start);
                    return;
                }

                NativeMethods.GetCursorPos(out var now);
                if (Math.Abs(now.X - start.X) + Math.Abs(now.Y - start.Y) < 25) return;

                poll.Stop();
                _previewOverlay?.Close();
                _previewOverlay = null;
            };
            poll.Start();
        }

        private MenuItem CreateShowSettingsMenuItem()
        {
            var item = new MenuItem { Header = "Show Settings" };
            item.Click += (_, _) => _showMainWindowAction?.Invoke();
            return item;
        }

        /// <summary>
        /// Creates the menu item used to toggle the run-at-startup registry setting.
        /// </summary>
        /// <returns>A configured MenuItem for the startup toggle.</returns>
        private MenuItem CreateStartupMenuItem()
        {
            var item = new MenuItem
            {
                Header = "Run at Startup",
                IsCheckable = true,
                IsChecked = StartupHelper.IsRunAtStartupEnabled()
            };

            item.Click += HandleStartupMenuItemClick;
            return item;
        }

        /// <summary>
        /// Handles the click event for the startup menu item, updating the registry and reverting UI state on failure.
        /// </summary>
        private void HandleStartupMenuItemClick(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem item) return;

            try
            {
                StartupHelper.SetRunAtStartup(item.IsChecked);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to toggle startup registry key.");
                item.IsChecked = !item.IsChecked; // Revert UI state on failure
            }
        }

        /// <summary>
        /// Creates the menu item used to exit the application.
        /// </summary>
        /// <returns>A configured MenuItem for exiting.</returns>
        private MenuItem CreateExitMenuItem()
        {
            var item = new MenuItem { Header = "Exit" };
            item.Click += (_, _) => _exitApplicationAction?.Invoke();
            return item;
        }

        /// <summary>
        /// Loads and sets the tray icon from resources.
        /// </summary>
        private void SetTrayIcon()
        {
            if (_notifyIcon == null) return;

            try
            {
                var iconUri = new Uri("pack://application:,,,/Assets/icon.ico", UriKind.RelativeOrAbsolute);
                _normalIcon = new System.Drawing.Icon(Application.GetResourceStream(iconUri)!.Stream, 32, 32);
                _pausedIcon = CreatePausedIcon(_normalIcon);
                _notifyIcon.Icon = _normalIcon;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load tray icon from resources.");
            }
        }

        /// <summary>
        /// Disposes the tray icon and releases resources.
        /// </summary>
        /// <summary>
        /// The normal icon with an amber pause badge in the corner, shown while the work mode is on.
        /// </summary>
        private static System.Drawing.Icon? CreatePausedIcon(System.Drawing.Icon baseIcon)
        {
            try
            {
                using var bitmap = new System.Drawing.Bitmap(32, 32);
                using (var g = System.Drawing.Graphics.FromImage(bitmap))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.DrawIcon(baseIcon, new System.Drawing.Rectangle(0, 0, 32, 32));
                    using var badge = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(255, 255, 176, 32));
                    using var bars = new System.Drawing.SolidBrush(System.Drawing.Color.Black);
                    g.FillEllipse(badge, 14, 14, 18, 18);
                    g.FillRectangle(bars, 19, 18, 3, 10);
                    g.FillRectangle(bars, 24, 18, 3, 10);
                }

                var handle = bitmap.GetHicon();
                // Clone so the icon owns its own handle data; the GDI handle itself is small and lives for the app's lifetime.
                return (System.Drawing.Icon)System.Drawing.Icon.FromHandle(handle).Clone();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Kon pauze-icoon niet maken; het normale icoon wordt gebruikt.");
                return null;
            }
        }

        public void Dispose()
        {
            _workModeService.Changed -= OnWorkModeChanged;
            _hotkeyService.Pressed -= OnHotkeyPressed;
            _hotkeyService.Changed -= OnHotkeyChanged;
            _hotkeyService.Dispose();
            _notifyIcon?.Dispose();
        }
    }
}