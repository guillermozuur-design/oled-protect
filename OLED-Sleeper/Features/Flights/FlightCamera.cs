namespace OLED_Sleeper.Features.Flights
{
    /// <summary>
    /// The slow, never-repeating drift of the flight map, so no road, place name or the pin stays on the same
    /// pixels. It is a mix of slow swings with unrelated periods (they practically never line up again), and the
    /// starting point is random each time the app starts. It depends only on the clock, so every screen —
    /// and the preview — shows the same moment of one continuous map.
    /// </summary>
    public static class FlightCamera
    {
        /// <summary>Camera state: offset of the view centre from the pin (as a fraction of the half view size),
        /// rotation of the map in degrees, and zoom factor.</summary>
        public readonly record struct State(double Dx, double Dy, double RotationDeg, double Zoom);

        /// <summary>Random phases, chosen once per app start.</summary>
        public static readonly double[] SessionPhases = RandomPhases(new Random());

        public static double[] RandomPhases(Random random) =>
            Enumerable.Range(0, 7).Select(_ => random.NextDouble() * 2 * Math.PI).ToArray();

        public static State At(double seconds, double[] phases)
        {
            static double Swing(double t, double period, double phase) => Math.Sin(2 * Math.PI * t / period + phase);

            double dx = 0.30 * Swing(seconds, 1373, phases[0]) + 0.12 * Swing(seconds, 523, phases[1]);
            double dy = 0.40 * Swing(seconds, 1129, phases[2]) + 0.15 * Swing(seconds, 409, phases[3]);
            double rotation = 9 * Swing(seconds, 1789, phases[4]) + 3 * Swing(seconds, 701, phases[5]);
            double zoom = 1 + 0.12 * Swing(seconds, 967, phases[6]);
            return new State(dx, dy, rotation, zoom);
        }

        public static State At(DateTime utc) => At((utc - DateTime.UnixEpoch).TotalSeconds, SessionPhases);
    }
}
