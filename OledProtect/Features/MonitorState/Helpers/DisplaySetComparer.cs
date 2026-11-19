using OledProtect.Features.MonitorInformation.Models;
using System.Windows;

namespace OledProtect.Features.MonitorState.Helpers
{
    // Decides whether two screen snapshots show the same desk. Rule:
    //  * both snapshots exist and hold the same number of screens;
    //  * every screen has exactly one counterpart under the same GDI device name;
    //  * counterparts are the same panel: equal hardware IDs, or one side's ID is unknown (lookups fail
    //    now and then, which must not look like a swap);
    //  * counterparts are laid out identically (position/size, DPI, primary flag, display number, topology).
    // DDC/CI capabilities are ignored, they are probed later and may come and go.
    public static class DisplaySetComparer
    {
        public static bool AreEquivalent(IReadOnlyList<MonitorInfo>? first, IReadOnlyList<MonitorInfo>? second)
        {
            if (first is null || second is null || first.Count != second.Count)
            {
                return false;
            }

            // The device name is part of the layout, so a valid counterpart always shares it: join on it.
            ILookup<string, MonitorInfo> slotsAfter = second.ToLookup(m => m.DeviceName, StringComparer.Ordinal);

            return first
                .GroupBy(m => m.DeviceName, StringComparer.Ordinal)
                .All(slot => SlotMatches([.. slot], [.. slotsAfter[slot.Key]]));
        }

        // Pairs the screens that share one device name (normally one on each side). Known IDs go first and
        // look for their exact twin before settling for a partner whose ID is unknown.
        private static bool SlotMatches(List<MonitorInfo> before, List<MonitorInfo> after)
        {
            if (before.Count != after.Count)
            {
                return false;
            }

            foreach (MonitorInfo screen in before.OrderBy(m => m.HardwareId.Length == 0))
            {
                Placement wanted = Placement.Of(screen);
                int hit = after.FindIndex(o => o.HardwareId == screen.HardwareId && Placement.Of(o) == wanted);
                if (hit < 0)
                {
                    hit = after.FindIndex(o => SamePanel(screen, o) && Placement.Of(o) == wanted);
                }

                if (hit < 0)
                {
                    return false;
                }

                after.RemoveAt(hit);
            }

            return true;
        }

        private static bool SamePanel(MonitorInfo a, MonitorInfo b) =>
            a.HardwareId.Length == 0 || b.HardwareId.Length == 0 || string.Equals(a.HardwareId, b.HardwareId, StringComparison.Ordinal);

        private readonly record struct Placement(Rect Area, uint Dpi, bool Primary, int Number, string Topology)
        {
            public static Placement Of(MonitorInfo m) => new(m.Bounds, m.Dpi, m.IsPrimary, m.DisplayNumber, m.TopologyLabel);
        }
    }
}
