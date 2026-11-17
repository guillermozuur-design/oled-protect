using OLED_Sleeper.Features.WorkMode.Services;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace OLED_Sleeper.UI.Views
{
    /// <summary>
    /// Quick panel in the style of Windows' Win+P: a small panel at the bottom right of the screen you are on.
    /// The hotkey opens it with the current mode highlighted; every further tap moves the highlight. Letting go
    /// of the modifier applies the highlighted choice (nothing happens if you did not move on). Afterwards the
    /// panel stays a few seconds so you can also just click; while the mouse is on it, it stays open. Esc closes.
    ///
    /// The panel never takes keyboard focus (the app you are in keeps it); keys are read with
    /// GetAsyncKeyState, so it works regardless of which window is in front.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public partial class QuickPanelWindow : Window
    {
        public static readonly TimeSpan TimedWorkMode = TimeSpan.FromHours(1);
        private static readonly TimeSpan Linger = TimeSpan.FromSeconds(3.5);
        private static readonly TimeSpan CloseAfterChoice = TimeSpan.FromSeconds(0.9);

        private sealed record Option(string Icon, string Title, string Subtitle, bool IsActive, Action Run);

        private const int VK_ESCAPE = 0x1B;
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        private readonly IWorkModeService _workMode;
        private readonly Action _openApp;
        private readonly Action _previewScreensaver;
        private readonly Action _previewFlights;
        private readonly Rect _monitorBounds;
        private readonly int _holdKey;
        private readonly DispatcherTimer _poll;
        private List<Option> _options = new();
        private readonly List<Border> _rows = new();
        private int _index;
        private int _indexAtPress;
        private bool _holding = true;
        private DateTime _closeAt = DateTime.MaxValue;
        private bool _closing;

        /// <param name="holdKey">Virtual key of the modifier held with the hotkey (e.g. VK_MENU for Alt).</param>
        /// <param name="hint">Short usage hint shown at the bottom.</param>
        public QuickPanelWindow(IWorkModeService workMode, Action openApp, Action previewScreensaver, Action previewFlights, Rect monitorBounds,
                                int holdKey, string hint)
        {
            InitializeComponent();
            _workMode = workMode;
            _openApp = openApp;
            _previewScreensaver = previewScreensaver;
            _previewFlights = previewFlights;
            _monitorBounds = monitorBounds;
            _holdKey = holdKey;
            HintText.Text = hint;

            BuildRows();
            _index = _indexAtPress = Math.Max(0, _options.FindIndex(o => o.IsActive));
            Highlight();

            SourceInitialized += (_, _) => MakeNonActivating();
            Loaded += (_, _) => PlaceBottomRight();

            _poll = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(25) };
            _poll.Tick += (_, _) => Poll();
            _poll.Start();
        }

        private void BuildRows()
        {
            bool on = _workMode.IsEnabled;
            var until = _workMode.EnabledUntil;
            _options = new List<Option>
            {
                new("", "Bescherming aan", "Stilstaande schermen op zwart, gedimd, sterren of vliegtuigen", !on, () => _workMode.SetEnabled(false)),
                new("", "Werkmodus", "Alle schermen blijven aan", on && until == null, () => _workMode.SetEnabled(true)),
                new("", until != null ? $"Werkmodus tot {until.Value.ToLocalTime():HH:mm}" : "Werkmodus 1 uur",
                    "Daarna gaat de bescherming vanzelf weer aan", on && until != null, () => _workMode.EnableFor(TimedWorkMode)),
                new("", "Sterren bekijken", "Op alle schermen; muis bewegen of een toets = stoppen", false, _previewScreensaver),
                new("", "Vliegtuigen bekijken", "Live kaart op alle schermen; muis of toets = stoppen", false, _previewFlights),
                new("", "App openen", "Instellingen per scherm", false, _openApp),
            };

            Rows.Children.Clear();
            _rows.Clear();
            for (int i = 0; i < _options.Count; i++)
            {
                if (i == 3)
                    Rows.Children.Add(new Border { Height = 1, Margin = new Thickness(8, 4, 8, 8), Background = (Brush)FindResource("SeparatorColorBrush") });

                var option = _options[i];
                var row = new Border { Style = (Style)FindResource("Row") };
                var grid = new DockPanel();

                var icon = new TextBlock
                {
                    Text = option.Icon,
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 18,
                    Width = 30,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = option.IsActive ? (Brush)FindResource("AccentColorBrush") : (Brush)FindResource("PrimaryForegroundColor")
                };
                DockPanel.SetDock(icon, Dock.Left);
                grid.Children.Add(icon);

                if (option.IsActive)
                {
                    var badge = new TextBlock { Text = "actief", FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Foreground = new SolidColorBrush(Color.FromRgb(0x4F, 0xC3, 0xF7)) };
                    DockPanel.SetDock(badge, Dock.Right);
                    grid.Children.Add(badge);
                }

                var texts = new StackPanel { Margin = new Thickness(6, 0, 0, 0) };
                texts.Children.Add(new TextBlock { Text = option.Title, FontSize = 14.5, Foreground = (Brush)FindResource("PrimaryForegroundColor") });
                texts.Children.Add(new TextBlock { Text = option.Subtitle, FontSize = 11, Margin = new Thickness(0, 1, 0, 0), TextWrapping = TextWrapping.Wrap, Foreground = (Brush)FindResource("SecondaryForegroundColor") });
                grid.Children.Add(texts);

                row.Child = grid;
                int index = i;
                row.MouseEnter += (_, _) => { _index = index; Highlight(); };
                row.MouseLeftButtonUp += (_, _) => Choose(index);
                Rows.Children.Add(row);
                _rows.Add(row);
            }
        }

        /// <summary>The hotkey was tapped again: move the highlight on (and hold-to-choose starts again).</summary>
        public void Next()
        {
            if (_closing) return;
            if (!_holding) { _holding = true; _indexAtPress = _index; }
            _index = (_index + 1) % _options.Count;
            Highlight();
        }

        private void Highlight()
        {
            for (int i = 0; i < _rows.Count; i++) _rows[i].Tag = i == _index ? "sel" : null;
        }

        private void Poll()
        {
            if (_closing) return;
            if (IsDown(VK_ESCAPE)) { CloseNow(); return; }

            if (_holding)
            {
                if (IsDown(_holdKey)) return;
                _holding = false;
                // Let go: do what is highlighted, unless nothing was moved (then just wait for a click).
                if (_index != _indexAtPress && !_options[_index].IsActive) Choose(_index);
                else _closeAt = DateTime.UtcNow + Linger;
                return;
            }

            if (IsMouseOver) _closeAt = DateTime.UtcNow + Linger;
            else if (DateTime.UtcNow >= _closeAt) CloseNow();
        }

        private void Choose(int index)
        {
            if (_closing) return;
            var option = _options[index];

            if (option.IsActive) { CloseNow(); return; }

            bool isMode = index <= 2;
            if (!isMode)
            {
                CloseNow();
                option.Run();
                return;
            }

            // Mode switch: apply, show the new state for a moment, then close.
            option.Run();
            BuildRows();
            _index = index;
            Highlight();
            _holding = false;
            _closeAt = DateTime.UtcNow + CloseAfterChoice;
        }

        private void CloseNow()
        {
            if (_closing) return;
            _closing = true;
            _poll.Stop();
            var fade = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(150));
            fade.Completed += (_, _) => Close();
            BeginAnimation(OpacityProperty, fade);
        }

        private void PlaceBottomRight()
        {
            // Monitor bounds are physical pixels; window positions are DIPs.
            var dpi = VisualTreeHelper.GetDpi(this);
            Left = (_monitorBounds.Right) / dpi.DpiScaleX - ActualWidth - 8;
            Top = (_monitorBounds.Bottom) / dpi.DpiScaleY - ActualHeight - 56; // rechtsonder boven de taakbalk, zoals Win+P
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(120)));
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
