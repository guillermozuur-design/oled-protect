using OledProtect.Features.MonitorDimming.Services.Interfaces;
using Serilog;

namespace OledProtect.Features.MonitorDimming.Services
{
    /// <summary>
    /// In-memory map of pre-dim brightness levels, written through to the state file on every change.
    /// Thread-safe.
    /// </summary>
    public sealed class OriginalBrightnessStore : IOriginalBrightnessStore
    {
        private readonly IMonitorBrightnessStateService _persistence;
        private readonly Dictionary<string, uint> _originals;

        // Covers the map and the save that follows each change, so an older snapshot can never overwrite a newer one.
        private readonly object _sync = new();

        public OriginalBrightnessStore(IMonitorBrightnessStateService persistence)
        {
            _persistence = persistence;
            _originals = persistence.LoadState();
        }

        public bool TryGetOriginal(string hardwareId, out uint rawValue)
        {
            lock (_sync)
            {
                return _originals.TryGetValue(hardwareId, out rawValue);
            }
        }

        public void RecordOriginal(string hardwareId, uint rawValue)
        {
            lock (_sync)
            {
                if (!_originals.TryAdd(hardwareId, rawValue))
                {
                    Log.Debug("Monitor {HardwareId} keeps its earlier recorded brightness {Brightness}.", hardwareId, _originals[hardwareId]);
                    return;
                }

                Persist();
            }
        }

        public void ReplaceOriginal(string hardwareId, uint rawValue)
        {
            lock (_sync)
            {
                _originals[hardwareId] = rawValue;
                Persist();
            }
        }

        public void RemoveOriginal(string hardwareId)
        {
            lock (_sync)
            {
                if (_originals.Remove(hardwareId))
                {
                    Persist();
                }
            }
        }

        public Dictionary<string, uint> GetAll()
        {
            lock (_sync)
            {
                return new(_originals);
            }
        }

        // Hands the persistence layer a copy, so it never sees the map change underneath it.
        private void Persist() => _persistence.SaveState(new Dictionary<string, uint>(_originals));
    }
}
