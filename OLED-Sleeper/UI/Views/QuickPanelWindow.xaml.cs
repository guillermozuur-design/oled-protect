using OLED_Sleeper.Features.WorkMode.Services;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace OLED_Sleeper.UI.Views
{
    /// <summary>
    /// Hold-to-choose quick panel, like Alt+Tab: it opens on the hotkey while the modifier is held, each extra
    /// tap of the hotkey moves the highlight, number keys or the mouse pick directly, and letting go of the
    /// modifier applies the highlighted choice and closes. Esc cancels.
    ///
    /// The panel never takes keyboard focus (the app you are in keeps it); keys are read with
    /// GetAsyncKeyState, so it works regardless of which window is in front.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public partial class QuickPanelWindow : Window
    {
        public static readonly TimeSpan TimedWorkMode = TimeSpan.FromHours(1);

        private enum Choice { Protect = 0, Work = 1, WorkHour = 2, Preview = 3, OpenApp = 4 }

        private const int VK_ESCAPE = 0x1B;
        private const int VK_1 = 0x31;
        private const int VK_NUMPAD1 = 0x61;
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        private readonly IWorkModeService _workMode;
        private readonly Action _openApp;
        private readonly Action _previewScreensaver;
        private readonly Rect _monitorBounds;
        private readonly int _holdKey;
        private readonly Border[] _items;
        private readonly DispatcherTimer _poll;
        private readonly bool[] _numberWasDown = new bool[5];
        private Choice _selected;
        private bool _finished;

        /// <param name="holdKey">Virtual key of the modifier that must stay held (e.g. VK_MENU for Alt).</param>
        /// <param name="hint">Short usage hint shown at the bottom.</param>
        public QuickPanelWindow(IWorkModeService workMode, Action openApp, Action previewScreensaver, Rect monitorBounds,
                                int holdKey, string hint)
        {
            InitializeComponent();
            _workMode = workMode;
            _openApp = openApp;
            _previewScreensaver = previewScreensaver;
            _monitorBounds = monitorBounds;
            _holdKey = holdKey;
            _items = new[] { Item0, Item1, Item2, Item3, Item4 };
            HintText.Text = hint;

            for (int i = 0; i < _items.Length; i++)
            {
                var choice = (Choice)i;
                _items[i].MouseEnter += (_, _) => Select(choice);
                _items[i].MouseLeftButtonUp += (_, _) => { Select(choice); Finish(apply: true); };
            }

            // Start on "the other mode", so a quick tap of the hotkey simply switches.
            Select(_workMode.IsEnabled ? Choice.Protect : Choice.Work);
            ShowCurrentState();

            SourceInitialized += (_, _) => MakeNonActivating();
            Loaded += (_, _) => CenterOnMonitor();

            _poll = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(25) };
            _poll.Tick += (_, _) => PollKeys();
            _poll.Start();
        }

        /// <summary>The hotkey was tapped again while the panel is open: move the highlight on.</summary>
        public void Next() => Select((Choice)(((int)_selected + 1) % _items.Length));

        private void Select(Choice choice)
        {
            _selected = choice;
            for (int i = 0; i < _items.Length; i++) _items[i].Tag = i == (int)choice ? "sel" : null;
        }

        private void ShowCurrentState()
        {
            bool on = _workMode.IsEnabled;
            var until = _workMode.EnabledUntil;
            Active0.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
            Active1.Visibility = on && until == null ? Visibility.Visible : Visibility.Collapsed;
            Active2.Visibility = on && until != null ? Visibility.Visible : Visibility.Collapsed;
            StatusText.Text = !on ? "bescherming actief"
                : until != null ? $"werkmodus tot {until.Value.ToLocalTime():HH:mm}"
                : "werkmodus";
        }

        private void PollKeys()
        {
            if (_finished) return;

            if (IsDown(VK_ESCAPE)) { Finish(apply: false); return; }

            for (int i = 0; i < _numberWasDown.Length; i++)
            {
                bool down = IsDown(VK_1 + i) || IsDown(VK_NUMPAD1 + i);
                if (down && !_numberWasDown[i]) Select((Choice)i);
                _numberWasDown[i] = down;
            }

            if (!IsDown(_holdKey)) Finish(apply: true);
        }

        private void Finish(bool apply)
        {
            if (_finished) return;
            _finished = true;
            _poll.Stop();
            Close();

            if (!apply) return;
            switch (_selected)
            {
                case Choice.Protect: _workMode.SetEnabled(false); break;
                case Choice.Work: _workMode.SetEnabled(true); break;
                case Choice.WorkHour: _workMode.EnableFor(TimedWorkMode); break;
                case Choice.Preview: _previewScreensaver(); break;
                case Choice.OpenApp: _openApp(); break;
            }
        }

        public void Cancel() => Finish(apply: false);

        private void CenterOnMonitor()
        {
            // Monitor bounds are physical pixels; window positions are DIPs.
            var dpi = VisualTreeHelper.GetDpi(this);
            Left = (_monitorBounds.Left + _monitorBounds.Width / 2) / dpi.DpiScaleX - ActualWidth / 2;
            Top = (_monitorBounds.Top + _monitorBounds.Height / 2) / dpi.DpiScaleY - ActualHeight / 2;
        }

        private void MakeNonActivating()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var style = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, new nint(style | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW));
        }

        private static bool IsDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint hWnd, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern nint SetWindowLongPtr(nint hWnd, int index, nint value);
    }
}
