using OledProtect.Features.MonitorDimming.Commands;
using OledProtect.Features.MonitorDimming.Handlers;

namespace OledProtect.Tests.Features.MonitorDimming.Handlers
{
    public sealed class ApplyUndimCommandHandlerTests
    {
        private readonly RecordingDimmer _dimmer = new();

        private Task Send(string id) => new ApplyUndimCommandHandler(_dimmer).HandleAsync(new ApplyUndimCommand { HardwareId = id });

        [Fact]
        public async Task Named_monitor_is_handed_to_the_dimmer_for_waking()
        {
            await Send("GSM-1");

            Assert.Equal(["undim GSM-1"], _dimmer.Calls);
        }

        [Fact]
        public async Task Dimmer_failure_is_swallowed_so_other_monitors_keep_working()
        {
            _dimmer.Broken.Add("GSM-1");

            await Send("GSM-1"); // must not throw

            Assert.Equal(["undim GSM-1"], _dimmer.Calls);
        }
    }
}
