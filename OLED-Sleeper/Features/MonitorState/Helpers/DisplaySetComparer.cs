using OLED_Sleeper.Features.MonitorInformation.Models;

namespace OLED_Sleeper.Features.MonitorState.Helpers
{
    // Tells whether two snapshots of the connected screens describe the same arrangement. DDC/CI
    // capabilities are deliberately not compared: they are probed later and may differ between reads.
    public static class DisplaySetComparer
    {
        // Matching runs in two rounds. Round one pairs screens by hardware ID, so a panel that Windows
        // renamed (\\.\DISPLAY1 -> DISPLAY2) still finds itself and the rename shows up as a difference.
        // Round two handles screens whose hardware ID could not be read in one of the snapshots: those
        // are paired by device name, so a flaky ID lookup is not mistaken for an unplugged screen.
        public static bool AreEquivalent(IReadOnlyList<MonitorInfo>? first, IReadOnlyList<MonitorInfo>? second)
        {
            if (first is null || second is null || first.Count != second.Count)
            {
                return false;
            }

            var claimed = new bool[second.Count];
            var leftOver = new List<MonitorInfo>();

            foreach (MonitorInfo screen in first)
            {
                int partner = screen.HardwareId.Length == 0
                    ? -1
                    : Claim(second, claimed, other => other.HardwareId == screen.HardwareId);

                if (partner < 0)
                {
                    leftOver.Add(screen);
                }
                else if (!LooksTheSame(screen, second[partner]))
                {
                    return false;
                }
            }

            foreach (MonitorInfo screen in leftOver)
            {
                // Two different resolved IDs under one name are two different panels, never a match.
                int partner = Claim(second, claimed, other =>
                    other.DeviceName == screen.DeviceName && (screen.HardwareId.Length == 0 || other.HardwareId.Length == 0));

                if (partner < 0 || !LooksTheSame(screen, second[partner]))
                {
                    return false;
                }
            }

            // Equal counts and every screen of the first snapshot paired means none of the second is left.
            return true;
        }

        // Marks the first unclaimed entry that fits as taken and returns its index, or -1.
        private static int Claim(IReadOnlyList<MonitorInfo> pool, bool[] claimed, Func<MonitorInfo, bool> fits)
        {
            for (int i = 0; i < pool.Count; i++)
            {
                if (!claimed[i] && fits(pool[i]))
                {
                    claimed[i] = true;
                    return i;
                }
            }

            return -1;
        }

        // Everything about how a paired screen is presented. The device name belongs here because DDC/CI
        // addresses screens by it: same panel under a new name still needs re-syncing.
        private static bool LooksTheSame(MonitorInfo a, MonitorInfo b) =>
            (a.DeviceName, a.Bounds, a.Dpi, a.IsPrimary, a.DisplayNumber, a.TopologyLabel)
            == (b.DeviceName, b.Bounds, b.Dpi, b.IsPrimary, b.DisplayNumber, b.TopologyLabel);
    }
}
