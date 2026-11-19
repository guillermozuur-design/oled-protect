using OledProtect.Features.MonitorInformation.Models;
using OledProtect.Features.MonitorState.Helpers;
using System.Windows;

namespace OledProtect.Tests.Features.MonitorState.Helpers
{
    // Rule under test: same count, every screen has one counterpart with the same device name, the
    // counterparts are the same panel (equal IDs or one ID unknown) and have the same layout.
    public class DisplaySetComparerTests
    {
        private const string Dell = @"MONITOR\DEL41A8\{4d36e96e-e325-11ce-bfc1-08002be10318}\0001";
        private const string Samsung = @"MONITOR\SAM7302\{4d36e96e-e325-11ce-bfc1-08002be10318}\0004";
        private const string Unknown = "";

        // A 1920x1080 panel at 100% on \\.\DISPLAY{slot}, placed 1920 px to the right per slot after the first.
        private static MonitorInfo Panel(int slot, string id, Action<MonitorInfo>? tweak = null)
        {
            var panel = new MonitorInfo
            {
                DeviceName = @"\\.\DISPLAY" + slot,
                DisplayNumber = slot,
                HardwareId = id,
                Bounds = new Rect((slot - 1) * 1920, 0, 1920, 1080),
                Dpi = 96,
                IsPrimary = slot == 1,
            };
            tweak?.Invoke(panel);
            return panel;
        }

        private static bool Equivalent(MonitorInfo[] before, MonitorInfo[] after) =>
            DisplaySetComparer.AreEquivalent(before, after);

        public static TheoryData<string, MonitorInfo[], MonitorInfo[]> UnchangedDesks => new()
        {
            { "same two panels", [Panel(1, Dell), Panel(2, Samsung)], [Panel(1, Dell), Panel(2, Samsung)] },
            { "listed in another order", [Panel(1, Dell), Panel(2, Samsung)], [Panel(2, Samsung), Panel(1, Dell)] },
            { "no panels at all", [], [] },
            { "id lost on the second read", [Panel(1, Dell), Panel(2, Samsung)], [Panel(1, Dell), Panel(2, Unknown)] },
            { "id found on the second read", [Panel(1, Unknown)], [Panel(1, Samsung)] },
            { "id unknown on both reads", [Panel(1, Unknown), Panel(2, Unknown)], [Panel(2, Unknown), Panel(1, Unknown)] },
            {
                "only the DDC/CI probe differs",
                [Panel(1, Dell, p => p.Capabilities = new DdcCiCapabilities(true, 100))],
                [Panel(1, Dell)]
            },
        };

        public static TheoryData<string, MonitorInfo[], MonitorInfo[]> ChangedDesks => new()
        {
            { "a panel was unplugged", [Panel(1, Dell), Panel(2, Samsung)], [Panel(1, Dell)] },
            { "a panel was plugged in", [Panel(1, Dell)], [Panel(1, Dell), Panel(2, Samsung)] },
            {
                "panels traded device names",
                [Panel(1, Dell), Panel(2, Samsung)],
                [Panel(2, Dell, p => p.Bounds = new Rect(0, 0, 1920, 1080)), Panel(1, Samsung, p => p.Bounds = new Rect(1920, 0, 1920, 1080))]
            },
            { "another panel took the name", [Panel(1, Dell)], [Panel(1, Samsung)] },
            { "panel moved", [Panel(1, Dell)], [Panel(1, Dell, p => p.Bounds = new Rect(-1920, 0, 1920, 1080))] },
            { "panel resized", [Panel(1, Dell)], [Panel(1, Dell, p => p.Bounds = new Rect(0, 0, 2560, 1440))] },
            { "primary moved", [Panel(1, Dell)], [Panel(1, Dell, p => p.IsPrimary = false)] },
            { "scaling changed", [Panel(1, Dell)], [Panel(1, Dell, p => p.Dpi = 144)] },
            { "display number changed", [Panel(1, Dell)], [Panel(1, Dell, p => p.DisplayNumber = 5)] },
            { "now duplicated", [Panel(1, Dell)], [Panel(1, Dell, p => p.TopologyLabel = "1|2")] },
            { "unknown panel moved", [Panel(1, Unknown)], [Panel(1, Unknown, p => p.Bounds = new Rect(0, 1080, 1920, 1080))] },
            {
                "known ids differ even if another name has an unknown id",
                [Panel(1, Dell), Panel(2, Unknown)],
                [Panel(1, Samsung), Panel(2, Unknown)]
            },
            { "a renamed panel with an unknown id", [Panel(1, Unknown)], [Panel(2, Unknown, p => p.Bounds = new Rect(0, 0, 1920, 1080))] },
        };

        [Theory]
        [MemberData(nameof(UnchangedDesks))]
        public void Reports_no_change(string scenario, MonitorInfo[] before, MonitorInfo[] after)
        {
            Assert.True(Equivalent(before, after), scenario);
            Assert.True(Equivalent(after, before), scenario + " (reversed)");
        }

        [Theory]
        [MemberData(nameof(ChangedDesks))]
        public void Reports_a_change(string scenario, MonitorInfo[] before, MonitorInfo[] after)
        {
            Assert.False(Equivalent(before, after), scenario);
            Assert.False(Equivalent(after, before), scenario + " (reversed)");
        }

        [Fact]
        public void Without_a_reading_there_is_never_a_match()
        {
            MonitorInfo[] desk = [Panel(1, Dell)];

            Assert.False(DisplaySetComparer.AreEquivalent(null, desk));
            Assert.False(DisplaySetComparer.AreEquivalent(desk, null));
            Assert.False(DisplaySetComparer.AreEquivalent(null, null));
        }

        [Fact]
        public void A_known_id_prefers_its_twin_over_an_unknown_one_under_the_same_name()
        {
            // Two entries under one name cannot come from Windows, but the pairing must still be one-to-one.
            MonitorInfo[] before = [Panel(1, Unknown), Panel(1, Dell)];
            MonitorInfo[] after = [Panel(1, Dell), Panel(1, Samsung)];

            Assert.True(Equivalent(before, after));
        }
    }
}
