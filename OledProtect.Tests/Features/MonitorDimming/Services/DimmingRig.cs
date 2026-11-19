using OledProtect.Features.MonitorDimming.Services;
using OledProtect.Features.MonitorDimming.Services.Interfaces;
using OledProtect.Features.MonitorInformation.Models;
using OledProtect.Features.MonitorInformation.Services.Interfaces;
using OledProtect.Tests.TestDoubles;

namespace OledProtect.Tests.Features.MonitorDimming.Services
{
    /// <summary>
    /// Everything a <see cref="MonitorDimmingService"/> test needs: a simulated bus, a list of attached screens
    /// that a test can change at any time, and a plain in-memory store for the saved levels.
    /// </summary>
    internal sealed class DimmingRig
    {
        public const string Oled = "GSM-OLED-A";
        public const string OledScreen = @"\\.\DISPLAY1";
        public const string Ips = "DEL-IPS-B";
        public const string IpsScreen = @"\\.\DISPLAY2";

        private readonly ScreenList _screens = new();

        public DimmingRig(BrightnessFadeOptions? fade = null)
        {
            Bus = new FakeDdcCiAccess();
            Saved = new SavedLevels();
            OledPanel = Bus.Wire(OledScreen, 80);
            IpsPanel = Bus.Wire(IpsScreen, 80);
            Attach((Oled, OledScreen, 255u), (Ips, IpsScreen, 255u));
            Service = new MonitorDimmingService(_screens, Bus, Saved, fade);
        }

        public FakeDdcCiAccess Bus { get; }

        public SavedLevels Saved { get; }

        public FakeDdcCiSession OledPanel { get; }

        public FakeDdcCiSession IpsPanel { get; }

        public MonitorDimmingService Service { get; }

        /// <summary>Replaces the attached screens; the third value is the reported brightness range.</summary>
        public void Attach(params (string Id, string Device, uint Range)[] screens) =>
            _screens.Current = screens
                .Select(s => new MonitorInfo { HardwareId = s.Id, DeviceName = s.Device, Capabilities = new DdcCiCapabilities(true, s.Range) })
                .ToList();

        /// <summary>Makes the next screen lookup fail with <paramref name="failure"/>.</summary>
        public void FailNextLookup(Exception failure) => _screens.FailNext = failure;

        // Only the cached list is used by the dimming service; the other members are not expected to be called.
        private sealed class ScreenList : IMonitorInfoManager
        {
            public IReadOnlyList<MonitorInfo> Current { get; set; } = [];

            public Exception? FailNext;

            public Task<IReadOnlyList<MonitorInfo>> GetCurrentMonitorsAsync() =>
                Interlocked.Exchange(ref FailNext, null) is { } failure
                    ? Task.FromException<IReadOnlyList<MonitorInfo>>(failure)
                    : Task.FromResult(Current);

            public Task<IReadOnlyList<MonitorInfo>> RefreshMonitorsAsync() => throw new NotSupportedException();

            public List<MonitorInfo> GetLatestMonitorsBasicInfo() => throw new NotSupportedException();

            public void EnrichMonitorInfoList(List<MonitorInfo> readings) => throw new NotSupportedException();
        }
    }

    /// <summary>
    /// An <see cref="IOriginalBrightnessStore"/> kept in memory, with the same keep-the-first rule for
    /// <see cref="RecordOriginal"/> as the real store, plus a log of what happened to it.
    /// </summary>
    internal sealed class SavedLevels : IOriginalBrightnessStore
    {
        private readonly Dictionary<string, uint> _levels = [];

        /// <summary>Runs inside <see cref="RecordOriginal"/>, before the level is stored.</summary>
        public Action<string, uint>? WhenRecording { get; set; }

        /// <summary>Calls in the form "record GSM-OLED-A 80", "replace ...", "remove ...".</summary>
        public List<string> Journal { get; } = [];

        public void Seed(string hardwareId, uint level) => _levels[hardwareId] = level;

        public bool Holds(string hardwareId) => _levels.ContainsKey(hardwareId);

        public bool TryGetOriginal(string hardwareId, out uint rawValue) => _levels.TryGetValue(hardwareId, out rawValue);

        public void RecordOriginal(string hardwareId, uint rawValue)
        {
            WhenRecording?.Invoke(hardwareId, rawValue);
            Journal.Add($"record {hardwareId} {rawValue}");
            _levels.TryAdd(hardwareId, rawValue);
        }

        public void ReplaceOriginal(string hardwareId, uint rawValue)
        {
            Journal.Add($"replace {hardwareId} {rawValue}");
            _levels[hardwareId] = rawValue;
        }

        public void RemoveOriginal(string hardwareId)
        {
            Journal.Add($"remove {hardwareId}");
            _levels.Remove(hardwareId);
        }

        public Dictionary<string, uint> GetAll() => new(_levels);
    }
}
