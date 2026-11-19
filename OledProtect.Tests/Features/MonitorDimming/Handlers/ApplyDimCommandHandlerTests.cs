using OledProtect.Features.MonitorDimming.Commands;
using OledProtect.Features.MonitorDimming.Handlers;

namespace OledProtect.Tests.Features.MonitorDimming.Handlers
{
    public sealed class ApplyDimCommandHandlerTests
    {
        private readonly RecordingDimmer _dimmer = new();

        private Task Send(string id, int level) =>
            new ApplyDimCommandHandler(_dimmer).HandleAsync(new ApplyDimCommand { HardwareId = id, DimLevel = level });

        [Fact]
        public async Task Monitor_and_level_arrive_unchanged_at_the_dimmer()
        {
            await Send("GSM-1", 15);

            Assert.Equal(["dim GSM-1 15"], _dimmer.Calls);
        }

        [Fact]
        public async Task Dimmer_failure_is_swallowed_so_the_idle_loop_keeps_going()
        {
            _dimmer.Broken.Add("GSM-1");

            await Send("GSM-1", 15); // must not throw

            Assert.Equal(["dim GSM-1 15"], _dimmer.Calls);
        }
    }
}
