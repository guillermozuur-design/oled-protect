using OLED_Sleeper.Features.WorkMode.Services;
using System.Windows;
using System.Windows.Input;

namespace OLED_Sleeper
{
    /// <summary>
    /// The main application window for OLED Sleeper.
    /// Handles window chrome and the custom title bar.
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Constructor

        private readonly GlobalHotkeyService _hotkeyService;

        private readonly OLED_Sleeper.Features.Sky.PanoramaSettings _panorama;

        private readonly OLED_Sleeper.Features.MonitorInformation.Services.Interfaces.IMonitorInfoManager _monitorInfoManager;

        public MainWindow(GlobalHotkeyService hotkeyService, OLED_Sleeper.Features.Sky.PanoramaSettings panorama,
                          OLED_Sleeper.Features.MonitorInformation.Services.Interfaces.IMonitorInfoManager monitorInfoManager,
                          OLED_Sleeper.Features.MonitorIdleDetection.Services.GlobalIdleSettings globalIdle)
        {
            _globalIdle = globalIdle;
            InitializeComponent();
            _hotkeyService = hotkeyService;
            _panorama = panorama;
            _monitorInfoManager = monitorInfoManager;
            _panorama.Changed += () => Dispatcher.BeginInvoke(() =>
                GapBox.Text = _panorama.GapCm.ToString("0.0", System.Globalization.CultureInfo.CurrentCulture));
            GapBox.Text = _panorama.GapCm.ToString("0.0", System.Globalization.CultureInfo.CurrentCulture);
            GlobalIdleCheck.IsChecked = _globalIdle.Enabled;
            GlobalIdleBox.Text = _globalIdle.Minutes.ToString();
            _hotkeyService.Changed += () => Dispatcher.BeginInvoke(ShowHotkey);
            Loaded += (_, _) => ShowHotkey();
        }

        #region Hotkey setting

        private void ShowHotkey()
        {
            if (HotkeyBox.IsKeyboardFocused) return;
            HotkeyBox.Text = _hotkeyService.HotkeyText;
            HotkeyStatus.Text = _hotkeyService.IsRegistered
                ? "ingedrukt houden, tik = volgende, loslaten = kiezen"
                : "in gebruik door een ander programma, kies een andere";
        }

        private void HotkeyBox_GotFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            _hotkeyService.Suspend();
            HotkeyBox.Text = "druk combinatie\u2026";
            HotkeyStatus.Text = "Esc = annuleren";
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
                HotkeyStatus.Text = "ingedrukt houden, tik = volgende, loslaten = kiezen";
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
                HotkeyStatus.Text = "gebruik minstens Ctrl, Alt, Shift of Win erbij";
                return;
            }

            uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
            bool ok = _hotkeyService.TryChange(modifiers, vk);
            HotkeyStatus.Text = ok
                ? "opgeslagen \u2713  ingedrukt houden, tik = volgende, loslaten = kiezen"
                : $"{GlobalHotkeyService.Format(modifiers, vk)} is al in gebruik, kies een andere";
            Keyboard.ClearFocus();
            HotkeyBox.Text = _hotkeyService.HotkeyText;
        }

        #endregion Hotkey setting

        #region General timer

        private readonly OLED_Sleeper.Features.MonitorIdleDetection.Services.GlobalIdleSettings _globalIdle;

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
                GlobalIdleStatus.Text = "vul 1 tot 600 minuten in";
                GlobalIdleBox.Text = _globalIdle.Minutes.ToString();
                GlobalIdleCheck.IsChecked = _globalIdle.Enabled;
                return;
            }

            _globalIdle.Set(GlobalIdleCheck.IsChecked == true, minutes);
            GlobalIdleStatus.Text = _globalIdle.Enabled
                ? $"opgeslagen \u2713  na {minutes} min zonder muis of toets gaan alle bewaakte schermen uit"
                : "uit: alleen de instellingen per scherm gelden";
        }

        #endregion General timer

        #region Panorama gap

        private void GapBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) CommitGap();
        }

        private void GapBox_LostFocus(object sender, KeyboardFocusChangedEventArgs e) => CommitGap();

        private void CommitGap()
        {
            var text = GapBox.Text.Trim().Replace(',', '.');
            if (double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var cm)
                && cm >= 0 && cm <= 10)
            {
                _panorama.GapCm = cm;
                GapStatus.Text = "opgeslagen \u2713  geldt vanaf de volgende keer dat de sterren verschijnen";
            }
            else
            {
                GapStatus.Text = "vul een getal tussen 0 en 10 in";
            }
            GapBox.Text = _panorama.GapCm.ToString("0.0", System.Globalization.CultureInfo.CurrentCulture);
        }

        private void Calibrate_Click(object sender, RoutedEventArgs e)
        {
            var bounds = _monitorInfoManager.GetLatestMonitorsBasicInfo().Select(m => m.Bounds).ToList();
            if (bounds.Count < 2 || bounds.All(b => OLED_Sleeper.Features.Sky.PanoramaLayout.Position(b).Count < 2))
            {
                GapStatus.Text = "kalibreren heeft twee schermen naast elkaar nodig";
                return;
            }
            new OLED_Sleeper.UI.Views.GapCalibration(_panorama, bounds).Show();
        }

        #endregion Panorama gap

        #endregion Constructor

        #region Title Bar & Window Controls

        /// <summary>
        /// Handles dragging the window when the custom title bar is clicked and dragged.
        /// </summary>
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        /// <summary>
        /// Minimizes the window when the minimize button is clicked.
        /// </summary>
        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        /// <summary>
        /// Toggles between maximized and normal window state when the maximize button is clicked.
        /// </summary>
        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = (WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;
        }

        /// <summary>
        /// Closes the window when the close button is clicked.
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion Title Bar & Window Controls
    }
}
