using OLED_Sleeper.Features.MonitorDimming.Services.Interfaces;

namespace OLED_Sleeper.Tests.TestDoubles
{
    /// <summary>
    /// A pretend panel behind a DDC/CI channel. It holds one brightness value; by default a write is what the
    /// next read returns, which is how a panel that applied the write behaves.
    /// </summary>
    public sealed class FakeDdcCiSession(uint initialBrightness) : IDdcCiSession
    {
        private uint _level = initialBrightness;

        /// <summary>Reads report "no answer".</summary>
        public bool IgnoresReads { get; set; }

        /// <summary>Writes are logged but refused, and the level stays as it was.</summary>
        public bool RefusesWrites { get; set; }

        /// <summary>When set, the panel reads back this value after any accepted write instead of the written one.</summary>
        public uint? SettlesAt { get; set; }

        /// <summary>Every value handed to <see cref="SetBrightness"/>, refused ones included, oldest first.</summary>
        public List<uint> WrittenBrightnessLevels { get; } = [];

        /// <summary>How often the channel was disposed; equal to the number of opens when nothing leaked.</summary>
        public int Disposals { get; private set; }

        public uint? GetBrightness() => IgnoresReads ? null : _level;

        public bool SetBrightness(uint rawValue)
        {
            WrittenBrightnessLevels.Add(rawValue);
            if (RefusesWrites) return false;

            _level = SettlesAt ?? rawValue;
            return true;
        }

        public void Dispose() => Disposals++;
    }
}
