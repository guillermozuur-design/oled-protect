using Serilog;

namespace OLED_Sleeper.Features.WorkMode.Services
{
    /// <inheritdoc />
    public class WorkModeService : IWorkModeService
    {
        private readonly object _lock = new();
        private bool _isEnabled;

        public bool IsEnabled
        {
            get { lock (_lock) return _isEnabled; }
        }

        public event Action<bool>? Changed;

        public void SetEnabled(bool enabled)
        {
            lock (_lock)
            {
                if (_isEnabled == enabled) return;
                _isEnabled = enabled;
            }

            Log.Information("Werkmodus {State}.", enabled ? "AAN" : "UIT");
            Changed?.Invoke(enabled);
        }

        public void Toggle()
        {
            bool target;
            lock (_lock) target = !_isEnabled;
            SetEnabled(target);
        }
    }
}
