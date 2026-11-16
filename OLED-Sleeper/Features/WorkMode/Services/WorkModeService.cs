using Serilog;

namespace OLED_Sleeper.Features.WorkMode.Services
{
    /// <inheritdoc />
    public class WorkModeService : IWorkModeService
    {
        private readonly object _lock = new();
        private bool _isEnabled;
        private DateTime? _enabledUntil;
        private Timer? _expiryTimer;

        public bool IsEnabled
        {
            get { lock (_lock) return _isEnabled; }
        }

        public DateTime? EnabledUntil
        {
            get { lock (_lock) return _enabledUntil; }
        }

        public event Action<bool>? Changed;

        public void SetEnabled(bool enabled)
        {
            lock (_lock)
            {
                bool hadTimer = _enabledUntil != null;
                CancelTimer();
                if (_isEnabled == enabled && !hadTimer) return;
                _isEnabled = enabled;
            }

            Log.Information("Werkmodus {State}.", enabled ? "AAN" : "UIT");
            Changed?.Invoke(enabled);
        }

        public void EnableFor(TimeSpan duration)
        {
            lock (_lock)
            {
                CancelTimer();
                _isEnabled = true;
                _enabledUntil = DateTime.UtcNow + duration;
                _expiryTimer = new Timer(_ => Expire(), null, duration, Timeout.InfiniteTimeSpan);
            }

            Log.Information("Werkmodus AAN voor {Minutes} minuten.", duration.TotalMinutes);
            Changed?.Invoke(true);
        }

        public void Toggle()
        {
            bool target;
            lock (_lock) target = !_isEnabled;
            SetEnabled(target);
        }

        private void Expire()
        {
            lock (_lock)
            {
                if (_enabledUntil == null || DateTime.UtcNow < _enabledUntil.Value.AddMilliseconds(-50)) return;
                CancelTimer();
                _isEnabled = false;
            }

            Log.Information("Werkmodus verlopen; bescherming weer actief.");
            Changed?.Invoke(false);
        }

        private void CancelTimer()
        {
            _expiryTimer?.Dispose();
            _expiryTimer = null;
            _enabledUntil = null;
        }
    }
}
