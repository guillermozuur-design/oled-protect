using OLED_Sleeper.Features.BrightnessSchedule.Services;
using OLED_Sleeper.Features.Flights;
using OLED_Sleeper.Features.KeepAwakeApps;
using OLED_Sleeper.Features.MonitorBlackout.Models;
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
    // The settings window: a personal header (current state, work-mode button) and four tabs — Schermen
    // (per-screen settings), Sterrenhemel (live preview, bezel gap), Vliegtuigen (pin, radius, live preview)
    // and Algemeen (hotkey, general timer, apps, start with Windows). Screen settings are saved automatically
    // as soon as they are valid. The window has no system frame; the title bar below is drawn in XAML.
    public partial class MainWindow
    {
        private readonly GlobalHotkeyService _hotkeyService;
        private readonly PanoramaSettings _panorama;
        private readonly IMonitorInfoManager _monitorInfoManager;
        private readonly GlobalIdleSettings _globalIdle;
        private readonly IWorkModeService _workMode;
        private readonly IMonitorSettingsFileService _settingsFile;
        private readonly ScreensaverPreview _preview;
        private readonly KeepAwakeAppsSettings _keepAwakeApps;
        private readonly FlightsSettings _flights;
        private readonly GlobalBrightnessSchedule _brightnessSchedule;
        private readonly OLED_Sleeper.Features.TaskbarDim.TaskbarDimSettings _taskbarDim;
        private FlightsView? _flightsPreview;
        private readonly DispatcherTimer _headerTimer;
        private readonly DispatcherTimer _autoSaveTimer;

        public MainWindow(GlobalHotkeyService hotkeyService, PanoramaSettings panorama, IMonitorInfoManager monitorInfoManager,
                          GlobalIdleSettings globalIdle, IWorkModeService workMode, IMonitorSettingsFileService settingsFile,
                          ScreensaverPreview preview, KeepAwakeAppsSettings keepAwakeApps, FlightsSettings flights,
                          GlobalBrightnessSchedule brightnessSchedule, OLED_Sleeper.Features.TaskbarDim.TaskbarDimSettings taskbarDim)
        {
            _keepAwakeApps = keepAwakeApps;
            _flights = flights;
            _brightnessSchedule = brightnessSchedule;
            _taskbarDim = taskbarDim;
            InitializeComponent();
            TaskbarDimCheck.IsChecked = _taskbarDim.Enabled;
            ScheduleCard.DataContext = new UI.ViewModels.BrightnessScheduleEditorViewModel(_brightnessSchedule);
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
            ShowFlightSettings();
            VersionText.Text = "versie " + AppVersion();

            _panorama.Changed += () => Dispatcher.BeginInvoke(() => GapBox.Text = FormatCm(_panorama.GapCm));
            _hotkeyService.Changed += () => Dispatcher.BeginInvoke(() => { ShowHotkey(); RefreshHeader(); });
            _workMode.Changed += _ => Dispatcher.BeginInvoke(RefreshHeader);
            _settingsFile.SettingsChanged += _ => Dispatcher.BeginInvoke(RefreshHeader);

            _headerTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
            _headerTimer.Tick += (_, _) => RefreshHeader();

            _autoSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
            _autoSaveTimer.Tick += (_, _) => AutoSave();

            Loaded += (_, _) => { ShowHotkey(); RefreshHeader(); BuildAppRows(); };
            // Minimised: the live preview stops (no drawing, no questions to the plane service).
            StateChanged += (_, _) => UpdateFlightsPreview();
            // "Starten met Windows" can also be changed from the tray menu: show the real state when coming back.
            Activated += (_, _) => StartupCheck.IsChecked = StartupHelper.IsRunAtStartupEnabled();
            IsVisibleChanged += (_, _) =>
            {
                // Only tick while the window is on screen.
                if (IsVisible) { RefreshHeader(); _headerTimer.Start(); _autoSaveTimer.Start(); }
                else { _headerTimer.Stop(); _autoSaveTimer.Stop(); AutoSave(); }
                UpdateFlightsPreview();
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
                WorkModeLabel.Text = "Werkmodus stoppen";
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
                WorkModeLabel.Text = "Werkmodus starten";
                WorkModeIcon.Text = "";
            }

            HotkeyHint.Text = _hotkeyService.IsRegistered ? $"of {_hotkeyService.HotkeyText} voor het snelmenu" : "";

            // Next step of the brightness schedule, if it is on.
            var next = (_brightnessSchedule.Enabled ? _brightnessSchedule.Entries : new())
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

        private void PreviewSky_Click(object sender, RoutedEventArgs e) => _preview.Show(allScreens: true);

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
                GapStatus.Text = "Opgeslagen ✓ — geldt vanaf de volgende keer dat de sterren of vliegtuigen verschijnen.";
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

        // ================= Vliegtuigen tab =================

        private void ShowFlightSettings()
        {
            PlaceBox.Text = _flights.PlaceName;
            PlaceStatus.Text = $"Nu: {_flights.PlaceName}. Typ een plaats, adres of postcode (of coördinaten) en kies Zoeken.";
            RadiusSlider.Value = _flights.RadiusKm;
            RadiusText.Text = $"{_flights.RadiusKm:0} km";
            FlightLabelsCheck.IsChecked = _flights.ShowLabels;
            bool tour = _flights.Mode == FlightsMode.Tour;
            TourCheck.IsChecked = tour;
            TourOptions.IsEnabled = tour;
            TourOptions.Opacity = tour ? 1 : 0.45;
            // The pin and its distance do nothing during the tour: greyed out, with a short note.
            PinOptions.IsEnabled = !tour;
            PinOptions.Opacity = tour ? 0.45 : 1;
            PinOptionsNote.Visibility = tour ? Visibility.Visible : Visibility.Collapsed;
            if (!CountriesBox.IsKeyboardFocusWithin) CountriesBox.Text = _flights.TourCountries;
            ShowCountriesStatus(_flights.TourCountries);
            FollowCheck.IsChecked = _flights.FollowFlights;
            SceneSlider.Value = _flights.SceneSeconds;
            SceneText.Text = $"{_flights.SceneSeconds} s";
            FlightsPreviewCaption.Text = tour ? "Live: rondreis langs vliegvelden" : $"Live rond {_flights.PlaceName}";
        }

        private void MainTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ReferenceEquals(e.OriginalSource, MainTabs)) UpdateFlightsPreview();
        }

        /// <summary>
        /// The live preview only runs while its tab is open and the window is visible, so the app never fetches
        /// aircraft in the background for nothing.
        /// </summary>
        private DispatcherTimer? _previewStatusTimer;

        /// <summary>The caption above the preview: what it shows, and what is wrong if something is missing.</summary>
        private void ShowPreviewStatus()
        {
            string caption = _flights.Mode == FlightsMode.Tour ? "Live: rondreis langs vliegvelden" : $"Live rond {_flights.PlaceName}";
            string status = _flightsPreview?.Status ?? "";
            FlightsPreviewCaption.Text = status.Length > 0 ? $"{caption} · {status}" : caption;
        }

        private void UpdateFlightsPreview()
        {
            bool wanted = IsVisible && WindowState != WindowState.Minimized && MainTabs.SelectedItem == FlightsTab;
            if (wanted && _flightsPreview == null)
            {
                // Text a little bigger than its size would give, so labels and messages stay readable.
                _flightsPreview = new FlightsView(_flights) { MinUi = 0.8 };
                FlightsPreviewHost.Content = _flightsPreview;
                _previewStatusTimer ??= new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => ShowPreviewStatus(), Dispatcher);
                _previewStatusTimer.Start();
            }
            else if (!wanted && _flightsPreview != null)
            {
                _previewStatusTimer?.Stop();
                _flightsPreview.Stop();
                FlightsPreviewHost.Content = null;
                _flightsPreview = null;
            }
        }

        private void PreviewFlights_Click(object sender, RoutedEventArgs e) => _preview.Show(allScreens: true, OverlayScene.Flights);

        private void PlaceBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) PlaceSearch_Click(sender, e);
        }

        private async void PlaceSearch_Click(object sender, RoutedEventArgs e)
        {
            string text = PlaceBox.Text.Trim();
            if (text.Length == 0) return;
            PlaceSearchButton.IsEnabled = false;
            PlaceStatus.Text = "Zoeken…";
            try
            {
                var result = await PlaceSearch.FindAsync(text);
                if (result == null)
                {
                    PlaceStatus.Text = $"\"{text}\" niet gevonden. Probeer een plaatsnaam, adres of postcode.";
                    return;
                }
                _flights.SetLocation(result.Latitude, result.Longitude, result.Name);
                ShowFlightSettings();
                PlaceStatus.Text = $"Opgeslagen ✓ — pin op {result.Name}. De kaart van deze plek wordt één keer opgehaald.";
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning("Place search failed: {Message}", ex.Message);
                PlaceStatus.Text = "Zoeken lukt nu niet (geen verbinding?). Je kunt ook coördinaten typen, zoals 52.09, 5.12.";
            }
            finally
            {
                PlaceSearchButton.IsEnabled = true;
            }
        }

        private void RadiusSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (RadiusText == null) return; // during InitializeComponent
            RadiusText.Text = $"{e.NewValue:0} km";
            if (!IsLoaded) return;
            // Apply when the slider comes to rest, so dragging it does not load a map for every step.
            _radiusDebounce ??= new DispatcherTimer(TimeSpan.FromMilliseconds(600), DispatcherPriority.Normal,
                (_, _) => { _radiusDebounce!.Stop(); _flights.SetRadius(RadiusSlider.Value); }, Dispatcher);
            _radiusDebounce.Stop();
            _radiusDebounce.Start();
        }

        private DispatcherTimer? _radiusDebounce;

        private void FlightLabels_Click(object sender, RoutedEventArgs e) => _flights.SetShowLabels(FlightLabelsCheck.IsChecked == true);

        private void Tour_Click(object sender, RoutedEventArgs e)
        {
            _flights.SetMode(TourCheck.IsChecked == true ? FlightsMode.Tour : FlightsMode.Home);
            ShowFlightSettings();
        }

        private void Follow_Click(object sender, RoutedEventArgs e) => _flights.SetFollowFlights(FollowCheck.IsChecked == true);

        private DispatcherTimer? _countriesDebounce;

        private void CountriesBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsLoaded || !CountriesBox.IsKeyboardFocusWithin) return;
            ShowCountriesStatus(CountriesBox.Text);
            // Save after a short pause in typing (each change restarts the tour).
            _countriesDebounce ??= new DispatcherTimer(TimeSpan.FromMilliseconds(900), DispatcherPriority.Normal,
                (_, _) =>
                {
                    _countriesDebounce!.Stop();
                    _flights.SetTourCountries(CountriesBox.Text);
                    CountriesStatus.Text += "  Opgeslagen ✓";
                }, Dispatcher);
            _countriesDebounce.Stop();
            _countriesDebounce.Start();
        }

        private void ShowCountriesStatus(string text)
        {
            var selection = TourSelection.From(text);
            string status = selection.Airports.Count == 0
                ? "Niets herkend: de rondreis gebruikt nu Nederland. Typ bijvoorbeeld: Spanje, Tenerife, JFK."
                : $"{string.Join(", ", selection.Recognised)}: {selection.Airports.Count} vliegvelden.";
            if (selection.Unknown.Count > 0)
                status += $" Niet gevonden: {string.Join(", ", selection.Unknown)} (controleer de spelling, of gebruik een vliegveldcode).";
            CountriesStatus.Text = status;
        }

        private DispatcherTimer? _sceneDebounce;

        private void SceneSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (SceneText == null) return; // during InitializeComponent
            SceneText.Text = $"{e.NewValue:0} s";
            if (!IsLoaded) return;
            _sceneDebounce ??= new DispatcherTimer(TimeSpan.FromMilliseconds(600), DispatcherPriority.Normal,
                (_, _) => { _sceneDebounce!.Stop(); _flights.SetSceneSeconds((int)SceneSlider.Value); }, Dispatcher);
            _sceneDebounce.Stop();
            _sceneDebounce.Start();
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
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            // Tab (and Shift+Tab) just move on to the next field, as everywhere else.
            if (key == Key.Tab) return;
            e.Handled = true;

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
                ? $"Aan ✓ — na {minutes} min zonder muis of toets krijgen alle bewaakte schermen hun ingestelde gedrag (behalve het scherm met een video die speelt)."
                : "Uit — alleen de instellingen per scherm gelden.";
        }

        private void TaskbarDim_Click(object sender, RoutedEventArgs e) => _taskbarDim.SetEnabled(TaskbarDimCheck.IsChecked == true);

        private void Startup_Click(object sender, RoutedEventArgs e)
        {
            bool wanted = StartupCheck.IsChecked == true;
            try
            {
                StartupHelper.SetRunAtStartup(wanted);
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning("Run at startup could not be changed: {Message}", ex.Message);
            }
            bool now = StartupHelper.IsRunAtStartupEnabled();
            StartupCheck.IsChecked = now;
            StartupStatus.Text = now == wanted
                ? "Start verborgen in het systeemvak bij het inloggen."
                : "Lukt niet: Windows staat het instellen nu niet toe. Probeer het later nog eens.";
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

        // ---- custom title bar (WindowStyle=None, so moving and the caption buttons are ours) ----

        private void OnTitleBarPressed(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && e.ChangedButton is MouseButton.Left)
                DragMove();
        }

        private void OnMinimizeClicked(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void OnMaximizeToggled(object sender, RoutedEventArgs e) =>
            WindowState = WindowState is WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

        // Goes through Closing, where MainWindowService turns it into "hide to tray".
        private void OnCloseClicked(object sender, RoutedEventArgs e) => Close();
    }
}
