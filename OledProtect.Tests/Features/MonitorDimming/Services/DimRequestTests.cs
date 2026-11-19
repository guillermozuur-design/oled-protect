using OledProtect.Features.MonitorDimming.Services;
using static OledProtect.Tests.Features.MonitorDimming.Services.DimmingRig;

namespace OledProtect.Tests.Features.MonitorDimming.Services
{
    public sealed class DimRequestTests
    {
        [Fact]
        public async Task Level_is_saved_before_anything_is_written_and_the_goal_is_scaled_to_the_panel()
        {
            var rig = new DimmingRig();
            int writesSeenAtSave = -1;
            rig.Saved.WhenRecording = (_, _) => writesSeenAtSave = rig.OledPanel.Attempts.Count;

            await rig.Service.DimMonitorAsync(Oled, 15);

            Assert.Equal(0, writesSeenAtSave);
            Assert.Equal(["record GSM-OLED-A 80"], rig.Saved.Journal);
            Assert.Equal([38u], rig.OledPanel.Attempts); // 15 % of 0-255 is 38.25
            Assert.Equal(38u, rig.OledPanel.Level);
        }

        [Fact]
        public async Task Panel_that_reports_no_range_is_given_the_percentage_itself()
        {
            var rig = new DimmingRig();
            rig.Attach((Oled, OledScreen, 0u));

            await rig.Service.DimMonitorAsync(Oled, 15);

            Assert.Equal([15u], rig.OledPanel.Attempts);
        }

        [Fact]
        public async Task Silent_panel_is_neither_saved_nor_written()
        {
            var rig = new DimmingRig();
            rig.OledPanel.Silent = true;

            await rig.Service.DimMonitorAsync(Oled, 15);

            Assert.Empty(rig.Saved.Journal);
            Assert.Empty(rig.OledPanel.Attempts);
            Assert.Equal(1, rig.OledPanel.Closed);
        }

        [Fact]
        public async Task Rejected_write_is_not_retried_and_the_saved_level_stays()
        {
            var rig = new DimmingRig();
            rig.OledPanel.Stubborn = true;

            await rig.Service.DimMonitorAsync(Oled, 15);

            Assert.True(rig.Saved.Holds(Oled));
            Assert.Equal([38u], rig.OledPanel.Attempts);
            Assert.Equal([OledScreen], rig.Bus.Dialled);
        }

        [Fact]
        public async Task Dimming_twice_keeps_the_level_from_before_the_first_dim()
        {
            var rig = new DimmingRig();

            await rig.Service.DimMonitorAsync(Oled, 15);
            await rig.Service.DimMonitorAsync(Oled, 5);

            Assert.True(rig.Saved.TryGetOriginal(Oled, out uint kept));
            Assert.Equal(80u, kept);
            Assert.Equal(13u, rig.OledPanel.Level); // 5 % of 255 is 12.75
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task Request_without_a_monitor_never_reaches_the_bus(string? id)
        {
            var rig = new DimmingRig();

            await rig.Service.DimMonitorAsync(id, 15);

            Assert.Empty(rig.Bus.Dialled);
        }

        [Fact]
        public async Task Monitor_that_is_not_attached_never_reaches_the_bus()
        {
            var rig = new DimmingRig();

            await rig.Service.DimMonitorAsync("ACR-UNPLUGGED", 15);

            Assert.Empty(rig.Bus.Dialled);
            Assert.Empty(rig.Saved.Journal);
        }

        [Fact]
        public async Task Wired_but_unreachable_monitor_is_not_saved()
        {
            var rig = new DimmingRig();
            rig.Attach(("ACR-DARK", @"\\.\DISPLAY9", 255u));

            await rig.Service.DimMonitorAsync("ACR-DARK", 15);

            Assert.Equal([@"\\.\DISPLAY9"], rig.Bus.Dialled);
            Assert.Empty(rig.Saved.Journal);
        }

        // ----- Fading -----

        [Fact]
        public async Task Fade_walks_down_in_even_steps_and_ends_on_the_goal()
        {
            var rig = new DimmingRig(new BrightnessFadeOptions(4, TimeSpan.Zero));
            rig.Attach((Oled, OledScreen, 100u));

            await rig.Service.DimMonitorAsync(Oled, 20);

            Assert.Equal([65u, 50u, 35u, 20u], rig.OledPanel.Attempts);
        }

        [Fact]
        public async Task Fade_rounds_in_between_levels_half_to_even()
        {
            // 80 -> 51 in four steps: 72.75, 65.5, 58.25, then the goal.
            var rig = new DimmingRig(new BrightnessFadeOptions(4, TimeSpan.Zero));

            await rig.Service.DimMonitorAsync(Oled, 20);

            Assert.Equal([73u, 66u, 58u, 51u], rig.OledPanel.Attempts);
        }

        [Fact]
        public async Task Fade_stops_its_in_between_steps_at_the_first_rejection_but_still_tries_the_goal()
        {
            var rig = new DimmingRig(new BrightnessFadeOptions(4, TimeSpan.Zero));
            rig.Attach((Oled, OledScreen, 100u));
            rig.OledPanel.Stubborn = true;

            await rig.Service.DimMonitorAsync(Oled, 20);

            Assert.Equal([65u, 20u], rig.OledPanel.Attempts);
        }

        [Fact]
        public async Task Fade_options_with_zero_steps_behave_as_a_single_write()
        {
            var rig = new DimmingRig(new BrightnessFadeOptions(0, TimeSpan.Zero));
            rig.Attach((Oled, OledScreen, 100u));

            await rig.Service.DimMonitorAsync(Oled, 20);

            Assert.Equal([20u], rig.OledPanel.Attempts);
        }
    }
}
