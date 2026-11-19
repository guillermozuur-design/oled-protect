using static OledProtect.Tests.Features.MonitorDimming.Services.DimmingRig;

namespace OledProtect.Tests.Features.MonitorDimming.Services
{
    // Requests for one monitor run one after another; requests for different monitors do not wait for each other.
    public sealed class PerMonitorQueueTests
    {
        private static readonly TimeSpan Patience = TimeSpan.FromMilliseconds(100);

        private readonly DimmingRig _rig = new();

        [Fact]
        public async Task Second_request_for_a_busy_monitor_waits_its_turn()
        {
            using var stall = _rig.Bus.StallNextOpen(OledScreen);
            var first = _rig.Service.DimMonitorAsync(Oled, 15);
            await stall.Reached;

            var second = _rig.Service.DimMonitorAsync(Oled, 15);

            Assert.False(await FinishesWithin(second, Patience));
            Assert.Single(_rig.Bus.Dialled);

            stall.Release();
            await Task.WhenAll(first, second);
            Assert.Equal(2, _rig.Bus.Dialled.Count);
        }

        [Fact]
        public async Task Wake_queued_behind_a_dim_runs_after_it()
        {
            _rig.Saved.Seed(Oled, 80);
            using var stall = _rig.Bus.StallNextOpen(OledScreen);
            var dim = _rig.Service.DimMonitorAsync(Oled, 15);
            await stall.Reached;

            var wake = _rig.Service.UndimMonitorAsync(Oled);

            Assert.False(await FinishesWithin(wake, Patience));

            stall.Release();
            await Task.WhenAll(dim, wake);
            Assert.Equal([38u, 80u], _rig.OledPanel.Attempts);
            Assert.False(_rig.Saved.Holds(Oled));
        }

        [Fact]
        public async Task Other_monitor_is_served_while_one_is_busy()
        {
            using var stall = _rig.Bus.StallNextOpen(OledScreen);
            var busy = _rig.Service.DimMonitorAsync(Oled, 15);
            await stall.Reached;

            await _rig.Service.DimMonitorAsync(Ips, 15);

            Assert.Equal([38u], _rig.IpsPanel.Attempts);
            Assert.Empty(_rig.OledPanel.Attempts);

            stall.Release();
            await busy;
        }

        [Fact]
        public async Task Failed_request_does_not_block_the_next_one()
        {
            _rig.FailNextLookup(new InvalidOperationException("display list unavailable"));

            await Assert.ThrowsAsync<InvalidOperationException>(() => _rig.Service.DimMonitorAsync(Oled, 15));
            await _rig.Service.DimMonitorAsync(Oled, 15);

            Assert.Equal([38u], _rig.OledPanel.Attempts);
        }

        [Fact]
        public async Task Caller_gets_control_back_while_the_panel_is_still_busy()
        {
            using var stall = _rig.Bus.StallNextOpen(OledScreen);

            var dim = _rig.Service.DimMonitorAsync(Oled, 15); // would hang right here if the work ran on this thread
            await stall.Reached;

            Assert.False(dim.IsCompleted);
            stall.Release();
            await dim;
        }

        private static async Task<bool> FinishesWithin(Task task, TimeSpan limit) =>
            await Task.WhenAny(task, Task.Delay(limit)) == task;
    }
}
