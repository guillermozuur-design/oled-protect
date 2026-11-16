using OLED_Sleeper.Features.BrightnessSchedule.Services;
using OLED_Sleeper.Features.KeepAwakeApps;
using OLED_Sleeper.Features.MonitorIdleDetection.Services;
using OLED_Sleeper.Features.MonitorInformation.Services.Interfaces;
using OLED_Sleeper.Features.Sky;
using OLED_Sleeper.Features.UserSettings.Services.Interfaces;
using OLED_Sleeper.Features.WorkMode.Services;
using OLED_Sleeper.Infrastructure.Helpers;
using OLED_Sleeper.UI.Services;
using OLED_Sleeper.UI.ViewModels;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace OLED_Sleeper
{
    /// <summary>
    /// The main window: a personal header (greeting, current state, work-mode button) and three tabs —
    /// Schermen (per-screen settings), Sterrenhemel (live preview, bezel gap) and Algemeen (hotkey, general
    /// timer, start with Windows). Screen settings are saved automatically as soon as they are valid.
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly GlobalHotkeyService _hotkeyService;
        private readonly PanoramaSettings _panorama;
        private readonly IMonitorInfoManager _monitorInfoManager;
        private readonly GlobalIdleSettings _globalIdle;
        private readonly IWorkModeService _workMode;
        private readonly IMonitorSettingsFileService _settingsFile;
        private readonly ScreensaverPreview _preview;
        private readonly KeepAwakeAppsSettings _keepAwakeApps;
        private readonly DispatcherTimer _headerTimer;
        private readonly DispatcherTimer _autoSaveTimer;

        public MainWindow(GlobalHotkeyService hotkeyService, PanoramaSettings panorama, IMonitorInfoManager monitorInfoManager,
                          GlobalIdleSettings globalIdle, IWorkModeService workMode, IMonitorSettingsFileService settingsFile,
                          ScreensaverPreview preview, KeepAwakeAppsSettings keepAwakeApps)
        {
            _keepAwakeApps = keepAwakeApps;
            InitializeComponent();
            _hotkeyService = hotkeyService;
            _panorama = panorama;
            _monitorInfoManager = monitorInfoManager;
            _globalIdle = globalIdle;
            _workMode = workMode;
            _settingsFile = settingsFile;
            _preview = preview;

            GapBox.Text = FormatCm(_panorama.GapCm);
            GlobalIdleCheck.IsChecked = _globalIdle.Enabled;
            GlobalIdleBox.Text = _globalIdle.Minutes.ToString();
            StartupCheck.IsChecked = StartupHelper.IsRunAtStartupEnabled();
            VersionText.Text = "versie " + AppVersion() + " · gebaseerd op OLED Sleeper van Quorthon13";

            _panorama.Changed += () => Dispatcher.BeginInvoke(() => GapBox.Text = FormatCm(_panorama.GapCm));
            _hotkeyService.Changed += () => Dispatcher.BeginInvoke(() => { ShowHotkey(); RefreshHeader(); });
            _workMode.Changed += _ => Dispatcher.BeginInvoke(RefreshHeader);
            _settingsFile.SettingsChanged += _ => Dispatcher.BeginInvoke(RefreshHeader);

            _headerTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
            _headerTimer.Tick += (_, _) => RefreshHeader();

            _autoSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
            _autoSaveTimer.Tick += (_, _) => AutoSave();

            Loaded += (_, _) => { ShowHotkey(); RefreshHeader(); BuildAppRows(); };
            IsVisibleChanged += (_, _) =>
            {
                // Only tick while the window is on screen.
                if (IsVisible) { RefreshHeader(); _headerTimer.Start(); _autoSaveTimer.Start(); }
                else { _headerTimer.Stop(); _autoSaveTimer.Stop(); AutoSave(); }
            };
        }

        /// <summary>The version from the git tag (MinVer), e.g. "3.0.0"; build details after '+' are left out.</summary>
        private static string AppVersion() =>
            System.Reflection.Assembly.GetExecutingAssembly()
                .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
                .FirstOrDefault()?.InformationalVersion.Split('+')[0] ?? "?";

        private static string FormatCm(double cm) => cm.ToString("0.0", CultureInfo.CurrentCulture);

        // ================= header =================

        private void RefreshHeader()
        {
            var settings = _settingsFile.LoadSettings();
            int managed = settings.Count(s => s.IsManaged);

            if (_workMode.IsEnabled)
            {
                var until = _workMode.EnabledUntil;
                GreetingText.Text = "Werkmodus";
                StatusText.Text = until != null
                    ? $"Alle schermen blijven aan tot {until.Value.ToLocalTime():HH:mm}"
                    : "Alle schermen blijven aan";
                StatusDot.Fill = (Brush)FindResource("StarGoldBrush");
                WorkModeLabel.Text = "Bescherming aan";
                WorkModeIcon.Text = "";
            }
            else
            {
                GreetingText.Text = managed == 0 ? "Nog niets ingesteld" : "Bescherming actief";
                StatusText.Text = managed switch
                {
                    0 => "Kies hieronder een scherm om te bewaken",
                    1 => "1 scherm bewaakt",
                    _ => $"{managed} schermen bewaakt"
                };
                StatusDot.Fill = (Brush)FindResource("AccentColorBrush");
                WorkModeLabel.Text = "Werkmodus aan";
                WorkModeIcon.Text = "";
            }

            HotkeyHint.Text = _hotkeyService.IsRegistered ? $"of {_hotkeyService.HotkeyText} voor het snelmenu" : "";

            // Next step of a brightness schedule, if one is on.
            var next = settings.Where(s => s.UseBrightnessSchedule)
                .SelectMany(s => s.BrightnessSchedule ?? new())
                .Select(e => (Time: BrightnessScheduleCalculator.ParseTime(e.Start), e.Brightness))
                .Where(e => e.Time.HasValue)
                .Select(e => (Time: e.Time!.Value, e.Brightness))
                .OrderBy(e => MinutesUntil(e.Time))
                .FirstOrDefault();
            if (next != default)
            {
                ScheduleText.Text = $"Volgende helderheid: {next.Brightness}% om {next.Time:HH\\:mm}";
                ScheduleText.Visibility = Visibility.Visible;
            }
            else
            {
                ScheduleText.Visibility = Visibility.Collapsed;
            }
        }

        private static double MinutesUntil(TimeOnly time)
        {
            var now = TimeOnly.FromDateTime(DateTime.Now);
            double diff = (time.ToTimeSpan() - now.ToTimeSpan()).TotalMinutes;
            return diff <= 0 ? diff + 24 * 60 : diff;
        }

        private void WorkMode_Click(object sender, RoutedEventArgs e) => _workMode.SetEnabled(!_workMode.IsEnabled);

        // ================= auto-save =================

        private void AutoSave()
        {
            if (DataContext is not MainViewModel vm || !vm.IsDirty) return;

            if (vm.Monitors.All(m => m.Configuration.IsValid))
            {
                if (vm.TrySaveChanges())
                    SaveStatusText.Text = $"Opgeslagen ✓  {DateTime.Now:HH:mm}";
            }
            else
            {
                SaveStatusText.Text = "Nog niet opgeslagen — controleer de rood gemarkeerde velden";
            }
        }

        // ================= Sterrenhemel tab =================

        private void PreviewSky_Click(object sender, RoutedEventArgs e) => _preview.Show();

        private void GapBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) CommitGap();
        }

        private void GapBox_LostFocus(object sender, KeyboardFocusChangedEventArgs e) => CommitGap();

        private void CommitGap()
        {
            var text = GapBox.Text.Trim().Replace(',', '.');
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var cm) && cm >= 0 && cm <= 10)
            {
                _panorama.GapCm = cm;
                GapStatus.Text = "Opgeslagen ✓ — geldt vanaf de volgende keer dat de sterren verschijnen.";
            }
            else
            {
                GapStatus.Text = "Vul een getal tussen 0 en 10 in.";
            }
            GapBox.Text = FormatCm(_panorama.GapCm);
        }

        private void Calibrate_Click(object sender, RoutedEventArgs e)
        {
            var bounds = _monitorInfoManager.GetLatestMonitorsBasicInfo().Select(m => m.Bounds).ToList();
            if (bounds.Count < 2 || bounds.All(b => PanoramaLayout.Position(b).Count < 2))
            {
                GapStatus.Text = "Kalibreren heeft twee schermen naast elkaar nodig.";
                return;
            }
            new UI.Views.GapCalibration(_panorama, bounds).Show();
        }

        // ================= Algemeen tab =================

        private const string HotkeyHelp = "Ingedrukt houden, tikken = volgende keuze, loslaten = kiezen.";

        private void ShowHotkey()
        {
            if (HotkeyBox.IsKeyboardFocused) return;
            HotkeyBox.Text = _hotkeyService.HotkeyText;
            HotkeyStatus.Text = _hotkeyService.IsRegistered ? HotkeyHelp : "In gebruik door een ander programma — kies een andere combinatie.";
        }

        private void HotkeyBox_GotFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            _hotkeyService.Suspend();
            HotkeyBox.Text = "druk combinatie…";
            HotkeyStatus.Text = "Druk nu je combinatie (met Ctrl, Alt, Shift of Win). Esc = annuleren.";
        }

        private void HotkeyBox_LostFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            _hotkeyService.Resume();
            HotkeyBox.Text = _hotkeyService.HotkeyText;
        }

        private void HotkeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            var key = e.Key == Key.System ? e.SystemKey : e.Key;

            if (key == Key.Escape)
            {
                HotkeyStatus.Text = HotkeyHelp;
                Keyboard.ClearFocus();
                return;
            }

            // Wait for the real key; a modifier on its own is not a combination yet.
            if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift
                or Key.LWin or Key.RWin)
                return;

            var modifiers = GlobalHotkeyService.ToHotkeyModifiers(Keyboard.Modifiers);
            if (modifiers == 0)
            {
                HotkeyStatus.Text = "Gebruik minstens Ctrl, Alt, Shift of Win erbij.";
                return;
            }

            uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
            bool ok = _hotkeyService.TryChange(modifiers, vk);
            HotkeyStatus.Text = ok
                ? "Opgeslagen ✓  " + HotkeyHelp
                : $"{GlobalHotkeyService.Format(modifiers, vk)} is al in gebruik — kies een andere.";
            Keyboard.ClearFocus();
            HotkeyBox.Text = _hotkeyService.HotkeyText;
        }

        private void GlobalIdle_Changed(object sender, RoutedEventArgs e) => CommitGlobalIdle();

        private void GlobalIdleBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) CommitGlobalIdle();
        }

        private void GlobalIdleBox_LostFocus(object sender, KeyboardFocusChangedEventArgs e) => CommitGlobalIdle();

        private void CommitGlobalIdle()
        {
            if (!int.TryParse(GlobalIdleBox.Text.Trim(), out var minutes) || minutes < 1 || minutes > 600)
            {
                GlobalIdleStatus.Text = "Vul 1 tot 600 minuten in.";
                GlobalIdleBox.Text = _globalIdle.Minutes.ToString();
                GlobalIdleCheck.IsChecked = _globalIdle.Enabled;
                return;
            }

            _globalIdle.Set(GlobalIdleCheck.IsChecked == true, minutes);
            GlobalIdleStatus.Text = _globalIdle.Enabled
                ? $"Aan ✓ — na {minutes} min zonder muis of toets gaan alle bewaakte schermen uit (behalve het scherm met een video die speelt)."
                : "Uit — alleen de instellingen per scherm gelden.";
        }

        private void Startup_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StartupHelper.SetRunAtStartup(StartupCheck.IsChecked == true);
            }
            catch
            {
                StartupCheck.IsChecked = StartupHelper.IsRunAtStartupEnabled();
            }
        }

        // ================= apps that keep the screen awake =================

        private void BuildAppRows()
        {
            AppRows.Children.Clear();
            var rules = _keepAwakeApps.Rules;
            foreach (var rule in rules)
            {
                var row = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };

                var scope = new ComboBox
                {
                    Width = 140,
                    Margin = new Thickness(12, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    IsEnabled = rule.Enabled,
                    ItemsSource = new[] { "dit scherm", "alle schermen" },
                    SelectedIndex = rule.Scope == KeepAwakeScope.AllScreens ? 1 : 0,
                    ToolTip = "Dit scherm = het scherm waar het venster van de app op staat. Alle schermen = zolang de app openstaat."
                };
                DockPanel.SetDock(scope, Dock.Right);

                if (!rule.IsPreset)
                {
                    var remove = new Button
                    {
                        Content = "\uE711",
                        FontFamily = new FontFamily("Segoe MDL2 Assets"),
                        FontSize = 10,
                        Margin = new Thickness(8, 0, 0, 0),
                        Padding = new Thickness(8, 6, 8, 6),
                        Style = (Style)FindResource("SubtleButton"),
                        ToolTip = "Verwijderen"
                    };
                    DockPanel.SetDock(remove, Dock.Right);
                    remove.Click += (_, _) => UpdateRules(list => list.RemoveAll(r => !r.IsPreset && r.Name == rule.Name));
                    row.Children.Add(remove);
                }
                row.Children.Add(scope);

                var toggle = new CheckBox
                {
                    Style = (Style)FindResource("Switch"),
                    IsChecked = rule.Enabled,
                    Content = rule.IsPreset ? rule.Name : $"{rule.Name}  ({string.Join(", ", rule.ProcessNames)})",
                    FontSize = 13.5,
                    VerticalAlignment = VerticalAlignment.Center
                };
                row.Children.Add(toggle);

                toggle.Click += (_, _) => UpdateRules(list =>
                {
                    var r = list.First(x => x.Name == rule.Name && x.IsPreset == rule.IsPreset);
                    r.Enabled = toggle.IsChecked == true;
                });
                scope.SelectionChanged += (_, _) => UpdateRules(list =>
                {
                    var r = list.First(x => x.Name == rule.Name && x.IsPreset == rule.IsPreset);
                    r.Scope = scope.SelectedIndex == 1 ? KeepAwakeScope.AllScreens : KeepAwakeScope.ThisScreen;
                }, rebuild: false);

                AppRows.Children.Add(row);
            }
        }

        private void UpdateRules(Action<List<KeepAwakeRule>> change, bool rebuild = true)
        {
            var list = _keepAwakeApps.Rules.ToList();
            change(list);
            _keepAwakeApps.SetRules(list);
            if (rebuild) BuildAppRows();
        }

        /// <summary>Shows the programs that have a window open right now; clicking one adds it (switched on).</summary>
        private void AddApp_Click(object sender, RoutedEventArgs e)
        {
            var known = _keepAwakeApps.Rules;
            var apps = AppWindowScanner.Scan()
                .GroupBy(w => w.ProcessName, StringComparer.OrdinalIgnoreCase)
                .Select(g => (Process: g.Key, Title: g.First().Title))
                .Where(a => !known.Any(r => r.Matches(a.Process)))
                .OrderBy(a => a.Process, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var menu = new ContextMenu { PlacementTarget = AddAppButton, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
            if (apps.Count == 0)
            {
                menu.Items.Add(new MenuItem { Header = "Geen andere open programma's gevonden — open de app eerst", IsEnabled = false });
            }
            foreach (var (process, title) in apps)
            {
                string shortTitle = title.Length > 50 ? title[..50] + "…" : title;
                var item = new MenuItem { Header = $"{process}  —  {shortTitle}" };
                item.Click += (_, _) => UpdateRules(list => list.Add(new KeepAwakeRule
                {
                    Name = process,
                    ProcessNames = new List<string> { process },
                    Enabled = true,
                    Scope = KeepAwakeScope.ThisScreen
                }));
                menu.Items.Add(item);
            }
            menu.IsOpen = true;
        }

        // ================= title bar =================

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void MaximizeButton_Click(object sender, RoutedEventArgs e) =>
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
