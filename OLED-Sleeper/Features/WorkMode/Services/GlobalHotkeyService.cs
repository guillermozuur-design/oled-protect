using OLED_Sleeper.Storage.Interfaces;
using Serilog;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace OLED_Sleeper.Features.WorkMode.Services
{
    /// <summary>
    /// The system-wide hotkey for the quick panel. Default Alt+` (the key under Esc), used like Alt+Tab: hold
    /// the modifier, tap the key to move on, let go to choose. The combination can be changed in the app and
    /// is remembered in <c>hotkey.json</c>. Must be registered on the UI thread, because the hotkey message
    /// arrives on that thread's message loop.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class GlobalHotkeyService : IDisposable
    {
        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_WIN = 0x0008;

        private const string FileName = "hotkey.json";
        private const int HotkeyId = 0x4F53;
        private const int WM_HOTKEY = 0x0312;
        private const uint VK_OEM_3 = 0xC0; // ` ~
        private const uint VK_O = 0x4F;

        public sealed class HotkeySetting
        {
            public uint Modifiers { get; set; }
            public uint Key { get; set; }
        }

        private readonly IAppDataFileStore _fileStore;
        private HwndSource? _source;
        private uint _modifiers = MOD_ALT;
        private uint _key = VK_OEM_3;
        private bool _suspended;

        public GlobalHotkeyService(IAppDataFileStore fileStore)
        {
            _fileStore = fileStore;
        }

        /// <summary>Fires on the UI thread every time the hotkey is pressed.</summary>
        public event Action? Pressed;

        /// <summary>Fires when the hotkey changed or could not be registered.</summary>
        public event Action? Changed;

        public bool IsRegistered { get; private set; }

        /// <summary>The hotkey in use, e.g. "Alt+`".</summary>
        public string HotkeyText => Format(_modifiers, _key);

        /// <summary>Virtual key of the modifier the user holds while choosing (Alt, Ctrl, Shift or Win).</summary>
        public int HoldKey => HoldKeyFor(_modifiers);

        /// <summary>Loads the saved combination (or the default) and registers it.</summary>
        public void Register()
        {
            if (_source != null) return;

            // A message-only window: invisible, just there to receive WM_HOTKEY.
            var parameters = new HwndSourceParameters("OLED-Sleeper-Hotkey") { ParentWindow = new nint(-3), WindowStyle = 0 };
            _source = new HwndSource(parameters);
            _source.AddHook(WndProc);

            var saved = _fileStore.Read<HotkeySetting>(FileName);
            if (saved != null && saved.Key != 0 && saved.Modifiers != 0)
            {
                (_modifiers, _key) = (saved.Modifiers, saved.Key);
            }

            if (!RegisterCurrent() && saved == null)
            {
                // Default taken by another program: fall back to Ctrl+Alt+O.
                (_modifiers, _key) = (MOD_CONTROL | MOD_ALT, VK_O);
                RegisterCurrent();
            }
            Changed?.Invoke();
        }

        /// <summary>
        /// Switches to a new combination. Returns false (and keeps the old one) when Windows refuses it,
        /// usually because another program already uses it.
        /// </summary>
        public bool TryChange(uint modifiers, uint key)
        {
            if (_source == null || modifiers == 0 || key == 0) return false;

            var (oldMods, oldKey) = (_modifiers, _key);
            UnregisterHotKey(_source.Handle, HotkeyId);
            (_modifiers, _key) = (modifiers, key);

            if (!RegisterCurrent())
            {
                (_modifiers, _key) = (oldMods, oldKey);
                RegisterCurrent();
                Changed?.Invoke();
                return false;
            }

            _fileStore.TryWrite(FileName, new HotkeySetting { Modifiers = modifiers, Key = key });
            _suspended = false;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Temporarily releases the hotkey, so it can be typed into the settings box.</summary>
        public void Suspend()
        {
            if (_source == null || _suspended) return;
            _suspended = true;
            UnregisterHotKey(_source.Handle, HotkeyId);
        }

        public void Resume()
        {
            if (_source == null || !_suspended) return;
            _suspended = false;
            RegisterCurrent();
        }

        private bool RegisterCurrent()
        {
            // No MOD_NOREPEAT: holding the modifier and tapping the key repeatedly must fire every time.
            IsRegistered = RegisterHotKey(_source!.Handle, HotkeyId, _modifiers, _key);
            if (IsRegistered)
                Log.Information("Sneltoets {Hotkey} voor het snelmenu geregistreerd.", HotkeyText);
            else
                Log.Warning("Sneltoets {Hotkey} kon niet worden geregistreerd (error {Error}); waarschijnlijk in gebruik.",
                    HotkeyText, Marshal.GetLastWin32Error());
            return IsRegistered;
        }

        private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyId)
            {
                Pressed?.Invoke();
                handled = true;
            }
            return nint.Zero;
        }

        /// <summary>Human-readable text for a combination, e.g. "Ctrl+Alt+O" or "Alt+`".</summary>
        public static string Format(uint modifiers, uint vk)
        {
            var parts = new List<string>();
            if ((modifiers & MOD_CONTROL) != 0) parts.Add("Ctrl");
            if ((modifiers & MOD_ALT) != 0) parts.Add("Alt");
            if ((modifiers & MOD_SHIFT) != 0) parts.Add("Shift");
            if ((modifiers & MOD_WIN) != 0) parts.Add("Win");
            parts.Add(KeyName(vk));
            return string.Join("+", parts);
        }

        /// <summary>Converts WPF modifier keys to RegisterHotKey modifier flags.</summary>
        public static uint ToHotkeyModifiers(ModifierKeys keys) =>
            ((keys & ModifierKeys.Control) != 0 ? MOD_CONTROL : 0) |
            ((keys & ModifierKeys.Alt) != 0 ? MOD_ALT : 0) |
            ((keys & ModifierKeys.Shift) != 0 ? MOD_SHIFT : 0) |
            ((keys & ModifierKeys.Windows) != 0 ? MOD_WIN : 0);

        private static string KeyName(uint vk)
        {
            switch (vk)
            {
                case VK_OEM_3: return "`";
                case 0xBA: return ";";
                case 0xBB: return "=";
                case 0xBC: return ",";
                case 0xBD: return "-";
                case 0xBE: return ".";
                case 0xBF: return "/";
                case 0xDB: return "[";
                case 0xDC: return "\\";
                case 0xDD: return "]";
                case 0xDE: return "'";
                case 0x20: return "Spatie";
            }
            if (vk >= 0x30 && vk <= 0x39) return ((char)vk).ToString();
            if (vk >= 0x41 && vk <= 0x5A) return ((char)vk).ToString();
            return KeyInterop.KeyFromVirtualKey((int)vk).ToString();
        }

        private static int HoldKeyFor(uint modifiers)
        {
            if ((modifiers & MOD_ALT) != 0) return 0x12;      // VK_MENU
            if ((modifiers & MOD_CONTROL) != 0) return 0x11;  // VK_CONTROL
            if ((modifiers & MOD_SHIFT) != 0) return 0x10;    // VK_SHIFT
            return 0x5B;                                       // VK_LWIN
        }

        public void Dispose()
        {
            if (_source == null) return;
            UnregisterHotKey(_source.Handle, HotkeyId);
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
