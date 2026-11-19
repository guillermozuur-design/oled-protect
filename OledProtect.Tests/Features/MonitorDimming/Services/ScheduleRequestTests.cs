using OledProtect.Features.MonitorDimming.Services;
using static OledProtect.Tests.Features.MonitorDimming.Services.DimmingRig;

namespace OledProtect.Tests.Features.MonitorDimming.Services
{
    public sealed class ScheduleRequestTests
    {
        private readonly DimmingRig _rig = new();

        [Fact]
        public async Task Awake_monitor_is_moved_to_the_scheduled_level()
        {
            bool applied = await _rig.Service.SetScheduledBrightnessAsync(Oled, 40);

            Assert.True(applied);
            Assert.Equal([102u], _rig.OledPanel.Attempts); // 40 % of 255
            Assert.Empty(_rig.Saved.Journal);
        }

        [Fact]
        public async Task Awake_monitor_fades_to_the_scheduled_level_when_fading_is_on()
        {
            var rig = new DimmingRig(new BrightnessFadeOptions(2, TimeSpan.Zero));
            rig.Attach((Oled, OledScreen, 100u));

            Assert.True(await rig.Service.SetScheduledBrightnessAsync(Oled, 40));

            Assert.Equal([60u, 40u], rig.OledPanel.Attempts);
        }

        [Fact]
        public async Task Dimmed_monitor_stays_dark_and_only_its_wake_level_changes()
        {
            _rig.Saved.Seed(Oled, 80);

            bool applied = await _rig.Service.SetScheduledBrightnessAsync(Oled, 40);

            Assert.True(applied);
            Assert.Empty(_rig.Bus.Dialled);
            Assert.Equal(["replace GSM-OLED-A 102"], _rig.Saved.Journal);
        }

        [Fact]
        public async Task Monitor_already_at_the_scheduled_level_is_not_written()
        {
            _rig.Attach((Oled, OledScreen, 100u));

            Assert.True(await _rig.Service.SetScheduledBrightnessAsync(Oled, 80));

            Assert.Empty(_rig.OledPanel.Attempts);
        }

        [Fact]
        public async Task Silent_panel_reports_failure()
        {
            _rig.OledPanel.Silent = true;

            Assert.False(await _rig.Service.SetScheduledBrightnessAsync(Oled, 40));
            Assert.Empty(_rig.OledPanel.Attempts);
        }

        [Fact]
        public async Task Rejected_write_reports_failure()
        {
            _rig.OledPanel.Stubborn = true;

            Assert.False(await _rig.Service.SetScheduledBrightnessAsync(Oled, 40));
        }

        [Fact]
        public async Task Unreachable_panel_reports_failure()
        {
            _rig.Attach(("ACR-DARK", @"\\.\DISPLAY9", 255u));

            Assert.False(await _rig.Service.SetScheduledBrightnessAsync("ACR-DARK", 40));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task Request_without_a_monitor_reports_failure_without_using_the_bus(string? id)
        {
            Assert.False(await _rig.Service.SetScheduledBrightnessAsync(id!, 40));
            Assert.Empty(_rig.Bus.Dialled);
        }
    }
}
