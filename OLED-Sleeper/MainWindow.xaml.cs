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

        public MainWindow(GlobalHotkeyService hotkeyService)
        {
            InitializeComponent();
            _hotkeyService = hotkeyService;
            _hotkeyService.Changed += () => Dispatcher.BeginInvoke(ShowHotkey);
            Loaded += (_, _) => ShowHotkey();
        }

        #region Hotkey setting

        private void ShowHotkey()
        {
            if (HotkeyBox.IsKeyboardFocused) return;
            HotkeyBox.Text = _hotkeyService.HotkeyText;
            if (!_hotkeyService.IsRegistered)
                HotkeyStatus.Text = "in gebruik door een ander programma, kies een andere";
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
