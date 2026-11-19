using static OledProtect.Tests.Features.MonitorDimming.Services.DimmingRig;

namespace OledProtect.Tests.Features.MonitorDimming.Services
{
    // Waking = undimming: write the saved level, read it back, and only then forget it.
    public sealed class WakeRequestTests
    {
        private readonly DimmingRig _rig = new();

        public WakeRequestTests() => _rig.Saved.Seed(Oled, 80);

        [Fact]
        public async Task Verified_wake_writes_the_saved_raw_level_once_and_forgets_it()
        {
            await _rig.Service.UndimMonitorAsync(Oled);

            Assert.Equal([80u], _rig.OledPanel.Attempts);
            Assert.Equal(["remove GSM-OLED-A"], _rig.Saved.Journal);
        }

        [Theory]
        [InlineData(2)]
        [InlineData(-2)]
        public async Task Read_back_within_two_steps_counts_as_verified(int drift)
        {
            _rig.OledPanel.Drift = drift;

            await _rig.Service.UndimMonitorAsync(Oled);

            Assert.Single(_rig.OledPanel.Attempts);
            Assert.False(_rig.Saved.Holds(Oled));
        }

        [Theory]
        [InlineData(3)]
        [InlineData(-3)]
        public async Task Read_back_three_steps_off_is_retried_three_times_then_given_up(int drift)
        {
            _rig.OledPanel.Drift = drift;

            await _rig.Service.UndimMonitorAsync(Oled);

            Assert.Equal(3, _rig.OledPanel.Attempts.Count);
            Assert.True(_rig.Saved.Holds(Oled));
        }

        [Fact]
        public async Task Unanswered_read_back_is_retried_three_times_then_given_up()
        {
            _rig.OledPanel.Silent = true;

            await _rig.Service.UndimMonitorAsync(Oled);

            Assert.Equal([80u, 80u, 80u], _rig.OledPanel.Attempts);
            Assert.True(_rig.Saved.Holds(Oled));
        }

        [Fact]
        public async Task Rejected_writes_are_retried_three_times_then_given_up()
        {
            _rig.OledPanel.Stubborn = true;

            await _rig.Service.UndimMonitorAsync(Oled);

            Assert.Equal(3, _rig.OledPanel.Attempts.Count);
            Assert.Empty(_rig.Saved.Journal);
        }

        [Fact]
        public async Task Every_round_opens_a_fresh_channel_and_closes_it_again()
        {
            _rig.OledPanel.Stubborn = true;

            await _rig.Service.UndimMonitorAsync(Oled);

            Assert.Equal([OledScreen, OledScreen, OledScreen], _rig.Bus.Dialled);
            Assert.Equal(3, _rig.OledPanel.Closed);
        }

        [Fact]
        public async Task Second_round_succeeding_is_enough()
        {
            _rig.OledPanel.RejectNext = 1;

            await _rig.Service.UndimMonitorAsync(Oled);

            Assert.Equal([80u, 80u], _rig.OledPanel.Attempts);
            Assert.False(_rig.Saved.Holds(Oled));
        }

        [Fact]
        public async Task Unreachable_panel_ends_the_wake_after_one_try_and_keeps_the_level()
        {
            _rig.Attach(("ACR-DARK", @"\\.\DISPLAY9", 255u));
            _rig.Saved.Seed("ACR-DARK", 80);

            await _rig.Service.UndimMonitorAsync("ACR-DARK");

            Assert.Equal([@"\\.\DISPLAY9"], _rig.Bus.Dialled);
            Assert.True(_rig.Saved.Holds("ACR-DARK"));
        }

        [Fact]
        public async Task Detached_monitor_keeps_its_level_without_touching_the_bus()
        {
            _rig.Attach((Ips, IpsScreen, 255u));

            await _rig.Service.UndimMonitorAsync(Oled);

            Assert.Empty(_rig.Bus.Dialled);
            Assert.True(_rig.Saved.Holds(Oled));
        }

        [Fact]
        public async Task Monitor_with_nothing_saved_is_left_alone()
        {
            await _rig.Service.UndimMonitorAsync(Ips);

            Assert.Empty(_rig.Bus.Dialled);
            Assert.Empty(_rig.Saved.Journal);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task Request_without_a_monitor_never_reaches_the_bus(string? id)
        {
            await _rig.Service.UndimMonitorAsync(id!);

            Assert.Empty(_rig.Bus.Dialled);
        }

        [Fact]
        public void Dimmed_monitors_are_reported_straight_from_the_store()
        {
            Assert.Equal(new Dictionary<string, uint> { [Oled] = 80 }, _rig.Service.GetDimmedMonitors());
        }
    }
}
