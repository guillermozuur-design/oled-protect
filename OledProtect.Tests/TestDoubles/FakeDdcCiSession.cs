using OledProtect.Features.MonitorDimming.Services.Interfaces;

namespace OledProtect.Tests.TestDoubles
{
    /// <summary>
    /// A simulated panel. It keeps a level, logs every write attempt and counts how often its channel was closed.
    /// Its knobs (<see cref="Silent"/>, <see cref="Stubborn"/>, <see cref="RejectNext"/>, <see cref="Drift"/>) make it misbehave.
    /// The same object stands for every channel opened to the panel, so its state survives reconnects.
    /// </summary>
    public sealed class FakeDdcCiSession : IDdcCiSession
    {
        private readonly List<uint> _attempts = [];

        public FakeDdcCiSession(uint level) => Level = level;

        /// <summary>What the panel shows right now.</summary>
        public uint Level { get; private set; }

        /// <summary>When true, every read goes unanswered.</summary>
        public bool Silent { get; set; }

        /// <summary>When true, every write is rejected and <see cref="Level"/> does not move.</summary>
        public bool Stubborn { get; set; }

        /// <summary>How many of the coming writes are rejected before the panel starts accepting again.</summary>
        public int RejectNext { get; set; }

        /// <summary>Added to each accepted write, imitating a panel that snaps to its own step size.</summary>
        public int Drift { get; set; }

        /// <summary>Each value a write was attempted with, rejected ones too, in order.</summary>
        public IReadOnlyList<uint> Attempts => _attempts;

        /// <summary>Number of channels closed; matches the number opened when nothing leaked.</summary>
        public int Closed { get; private set; }

        uint? IDdcCiSession.GetBrightness() => Silent ? null : Level;

        bool IDdcCiSession.SetBrightness(uint rawValue)
        {
            _attempts.Add(rawValue);
            bool rejected = Stubborn || RejectNext > 0;
            if (RejectNext > 0) RejectNext--;
            if (!rejected) Level = (uint)(rawValue + Drift);
            return !rejected;
        }

        void IDisposable.Dispose() => Closed++;
    }
}
