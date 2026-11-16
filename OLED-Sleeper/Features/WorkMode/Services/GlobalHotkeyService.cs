using Serilog;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace OLED_Sleeper.Features.WorkMode.Services
{
    /// <summary>
    /// Registers a system-wide hotkey (default Ctrl+Alt+O) that toggles the work mode. Must be created and
    /// disposed on the UI thread, because the hotkey message arrives on that thread's message loop.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class GlobalHotkeyService : IDisposable
    {
        public const string HotkeyText = "Ctrl+Alt+O";

        private const int HotkeyId = 0x4F53; // "OS"
        private const int WM_HOTKEY = 0x0312;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_NOREPEAT = 0x4000;
        private const uint VK_O = 0x4F;

        private readonly IWorkModeService _workModeService;
        private HwndSource? _source;

        public GlobalHotkeyService(IWorkModeService workModeService)
        {
            _workModeService = workModeService;
        }

        /// <summary>True when Windows accepted the hotkey; false when another program already uses it.</summary>
        public bool IsRegistered { get; private set; }

        public void Register()
        {
            if (_source != null) return;

            // A message-only window: invisible, just there to receive WM_HOTKEY.
            var parameters = new HwndSourceParameters("OLED-Sleeper-Hotkey") { ParentWindow = new nint(-3), WindowStyle = 0 };
            _source = new HwndSource(parameters);
            _source.AddHook(WndProc);

            IsRegistered = RegisterHotKey(_source.Handle, HotkeyId, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, VK_O);
            if (IsRegistered)
                Log.Information("Sneltoets {Hotkey} voor werkmodus geregistreerd.", HotkeyText);
            else
                Log.Warning("Sneltoets {Hotkey} kon niet worden geregistreerd (error {Error}); waarschijnlijk in gebruik door een ander programma.",
                    HotkeyText, Marshal.GetLastWin32Error());
        }

        private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyId)
            {
                _workModeService.Toggle();
                handled = true;
            }
            return nint.Zero;
        }

        public void Dispose()
        {
            if (_source == null) return;
            if (IsRegistered) UnregisterHotKey(_source.Handle, HotkeyId);
            _source.RemoveHook(WndProc);
            _source.Dispose();
            _source = null;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(nint hWnd, int id);
    }
}
