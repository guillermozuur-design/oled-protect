using OledProtect.Storage.Interfaces;

namespace OledProtect.Features.MonitorIdleDetection.Services
{
    /// <summary>
    /// The general timer: after this many minutes without any mouse movement or key press, every managed
    /// monitor goes to its idle behaviour (blackout, dim or stars), whatever its own conditions say.
    /// Remembered in <c>general.json</c>.
    /// </summary>
    public class GlobalIdleSettings
    {
        private const string FileName = "general.json";

        private sealed class Stored
        {
            public bool Enabled { get; set; }
            public int Minutes { get; set; } = 10;
        }

        private readonly IAppDataFileStore? _fileStore;
        private volatile bool _enabled;
        private volatile int _minutes = 10;

        public GlobalIdleSettings(IAppDataFileStore? fileStore = null)
        {
            _fileStore = fileStore;
            var stored = _fileStore?.Read<Stored>(FileName);
            if (stored != null)
            {
                _enabled = stored.Enabled;
                _minutes = Math.Clamp(stored.Minutes, 1, 600);
            }
        }

        public bool Enabled => _enabled;

        public int Minutes => _minutes;

        public void Set(bool enabled, int minutes)
        {
            _enabled = enabled;
            _minutes = Math.Clamp(minutes, 1, 600);
            _fileStore?.TryWrite(FileName, new Stored { Enabled = _enabled, Minutes = _minutes });
        }

        /// <summary>True when the general timer has run out: nothing may keep a monitor awake any more.</summary>
        public bool HasExpired(uint systemIdleMilliseconds) =>
            _enabled && systemIdleMilliseconds >= (long)_minutes * 60_000;
    }
}
