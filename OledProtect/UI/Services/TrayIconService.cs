using Hardcodet.Wpf.TaskbarNotification;
using OledProtect.Features.BrightnessSchedule.Services;
using OledProtect.Features.MonitorBlackout.Models;
using OledProtect.Features.MonitorInformation.Services.Interfaces;
using OledProtect.Features.Sky;
using OledProtect.Features.WorkMode.Services;
using OledProtect.Infrastructure.Helpers;
using OledProtect.UI.Services.Interfaces;
using OledProtect.UI.Views;
using Serilog;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;

namespace OledProtect.UI.Services
{
    // Everything the app shows in the notification area:
    //  - a single click (or the global hotkey) opens the quick panel on the screen under the mouse,
    //  - a double click opens the settings window,
    //  - the right-click menu has work mode, previews, settings, autostart and exit,
    //  - icon and tooltip follow the work mode (pause badge, "tot HH:mm").
    public class TrayIconService : ITrayIconService
    {
        private const string AppName = "OLED Protect";

        private readonly IWorkModeService _workMode;
        private readonly IMonitorInfoManager _monitors;
        private readonly GlobalHotkeyService _hotkey;
        private readonly ScreensaverPreview _preview;
        private readonly ManualBrightnessService? _brightness;

        private TaskbarIcon? _tray;
        private MenuItem? _workModeToggle;
        private System.Drawing.Icon? _iconNormal;
        private System.Drawing.Icon? _iconPaused;
        private QuickPanelWindow? _panel;
        private Action _openSettings = () => { };
        private Action _quit = () => { };

        // PanoramaSettings is not used here, but stays a dependency so the DI registration is unchanged.
        public TrayIconService(IWorkModeService workModeService, IMonitorInfoManager monitorInfoManager, GlobalHotkeyService hotkeyService,
                               PanoramaSettings panorama, ScreensaverPreview preview,
                               ManualBrightnessService? brightness = null)
        {
            _workMode = workModeService;
            _monitors = monitorInfoManager;
            _hotkey = hotkeyService;
            _preview = preview;
            _brightness = brightness;
        }

        public void Initialize(Action openSettings, Action quit)
        {
            ArgumentNullException.ThrowIfNull(openSettings);
            ArgumentNullException.ThrowIfNull(quit);
            _openSettings = openSettings;
            _quit = quit;

            _tray = new TaskbarIcon { ToolTipText = AppName };
            _tray.TrayLeftMouseUp += (_, _) => { if (_panel is null) OpenQuickPanel(); };
            _tray.TrayMouseDoubleClick += (_, _) =>
            {
                _panel?.Close();
                _openSettings();
            };
            _tray.ContextMenu = BuildMenu();

            _iconNormal = TrayIconArt.LoadAppIcon();
            if (_iconNormal is not null)
            {
                _iconPaused = TrayIconArt.WithPauseBadge(_iconNormal);
                _tray.Icon = _iconNormal;
            }

            _hotkey.Pressed += HotkeyPressed;
            _hotkey.Changed += HotkeyChanged;
            _hotkey.Register();
            _workMode.Changed += WorkModeChanged;

            Refresh(_workMode.IsEnabled, announce: false);
        }

        public void Dispose()
        {
            _workMode.Changed -= WorkModeChanged;
            _hotkey.Pressed -= HotkeyPressed;
            _hotkey.Changed -= HotkeyChanged;
            _hotkey.Dispose();
            _tray?.Dispose();
        }

        // ---- menu ----

        private ContextMenu BuildMenu()
        {
            _workModeToggle = new MenuItem { Header = "Werkmodus (alles aan)", IsCheckable = true, IsChecked = _workMode.IsEnabled };
            _workModeToggle.Click += (_, _) => _workMode.SetEnabled(_workModeToggle.IsChecked);

            var autostart = new MenuItem { Header = "Starten met Windows", IsCheckable = true, IsChecked = StartupHelper.IsRunAtStartupEnabled() };
            autostart.Click += (_, _) => ApplyAutostart(autostart);

            var menu = new ContextMenu();
            menu.Items.Add(_workModeToggle);
            menu.Items.Add(new Separator());
            menu.Items.Add(Entry("Sterren bekijken", PreviewStars));
            menu.Items.Add(Entry("Vliegtuigen bekijken", PreviewFlights));
            menu.Items.Add(Entry("Instellingen openen", () => _openSettings()));
            menu.Items.Add(autostart);
            menu.Items.Add(new Separator());
            menu.Items.Add(Entry("Afsluiten", () => _quit()));

            // The settings window can change autostart too, so re-read it whenever the menu opens.
            menu.Opened += (_, _) => autostart.IsChecked = StartupHelper.IsRunAtStartupEnabled();
            return menu;
        }

        private static MenuItem Entry(string header, Action onClick)
        {
            var item = new MenuItem { Header = header };
            item.Click += (_, _) => onClick();
            return item;
        }

        // The check mark has already flipped when Click fires; flip it back if the registry write fails.
        private static void ApplyAutostart(MenuItem toggle)
        {
            bool wanted = toggle.IsChecked;
            try
            {
                StartupHelper.SetRunAtStartup(wanted);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not change the run-at-startup setting.");
                toggle.IsChecked = !wanted;
            }
        }

        // Real overlay windows on every monitor, as one continuous scene, until the mouse moves.
        private void PreviewStars() => _preview.Show(allScreens: true);

        private void PreviewFlights() => _preview.Show(allScreens: true, OverlayScene.Flights);

        // ---- quick panel ----

        // Alt+Tab style: the first press opens the panel, further presses while the modifier is
        // still held step through its entries.
        private void HotkeyPressed()
        {
            if (_panel is null)
                OpenQuickPanel();
            else
                _panel.Next();
        }

        private void OpenQuickPanel()
        {
            var all = _monitors.GetLatestMonitorsBasicInfo();
            Point mouse = MousePosition();
            var screen = all.FirstOrDefault(m => m.Bounds.Contains(mouse)) ?? all.FirstOrDefault();
            if (screen is null)
                return;

            _panel = new QuickPanelWindow(
                _workMode,
                () => _openSettings(),
                PreviewStars,
                PreviewFlights,
                screen.Bounds,
                _hotkey.HoldKey,
                QuickPanelHint(_hotkey.HotkeyText),
                _brightness);
            _panel.Closed += (_, _) => _panel = null;
            _panel.Show();
        }

        // "Ctrl+Alt+Space" -> hold "Ctrl+Alt", tap "Space".
        private static string QuickPanelHint(string hotkey)
        {
            int split = hotkey.LastIndexOf('+');
            string hold = split < 0 ? hotkey : hotkey[..split];
            string tap = hotkey[(split + 1)..];
            return $"{hold} vasthouden, {tap} = volgende · Esc = sluiten";
        }

        // ---- work mode feedback ----

        private void HotkeyChanged() => Refresh(_workMode.IsEnabled, announce: false);

        // Raised from whatever thread changed the mode; the tray must be touched on the UI thread.
        private void WorkModeChanged(bool on) =>
            Application.Current?.Dispatcher.BeginInvoke(() => Refresh(on, announce: true));

        private void Refresh(bool on, bool announce)
        {
            if (_tray is null)
                return;

            if (_workModeToggle is not null)
                _workModeToggle.IsChecked = on;

            string shortcut = _hotkey.IsRegistered ? _hotkey.HotkeyText : "sneltoets niet beschikbaar";
            string until = _workMode.EnabledUntil is { } end ? $" tot {end.ToLocalTime():HH:mm}" : "";

            _tray.ToolTipText = on
                ? $"{AppName} – WERKMODUS{until}: alles blijft aan ({shortcut} = snelmenu)"
                : $"{AppName} – bescherming actief ({shortcut} = snelmenu)";

            if ((on ? _iconPaused : _iconNormal) is { } image)
                _tray.Icon = image;

            if (!announce)
                return;

            if (on)
                _tray.ShowBalloonTip("Werkmodus aan" + until, "Alle schermen blijven aan.", BalloonIcon.Info);
            else
                _tray.ShowBalloonTip("Werkmodus uit", "Inbrandbescherming is weer actief.", BalloonIcon.Info);
        }

        // ---- Win32 ----

        private static Point MousePosition() =>
            GetCursorPos(out var p) ? new Point(p.X, p.Y) : new Point();

        [StructLayout(LayoutKind.Sequential)]
        private struct ScreenPoint
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out ScreenPoint point);
    }
}
