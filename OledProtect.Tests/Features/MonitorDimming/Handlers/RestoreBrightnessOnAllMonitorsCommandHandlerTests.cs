using OledProtect.Features.MonitorDimming.Commands;
using OledProtect.Features.MonitorDimming.Handlers;

namespace OledProtect.Tests.Features.MonitorDimming.Handlers
{
    public sealed class RestoreBrightnessOnAllMonitorsCommandHandlerTests
    {
        private readonly RecordingDimmer _dimmer = new();

        private Task Startup() =>
            new RestoreBrightnessOnAllMonitorsCommandHandler(_dimmer).HandleAsync(new RestoreBrightnessOnAllMonitorsCommand());

        [Fact]
        public async Task Clean_previous_run_means_no_wake_calls()
        {
            await Startup();

            Assert.Empty(_dimmer.Calls);
        }

        [Fact]
        public async Task Each_monitor_left_dimmed_is_woken_exactly_once()
        {
            _dimmer.LeftDimmed["GSM-1"] = 80;
            _dimmer.LeftDimmed["DEL-2"] = 120;

            await Startup();

            Assert.Equal(["undim DEL-2", "undim GSM-1"], _dimmer.Calls.Order());
        }

        [Fact]
        public async Task Dimmer_failure_is_passed_on_to_the_startup_code()
        {
            _dimmer.LeftDimmed["GSM-1"] = 80;
            _dimmer.Broken.Add("GSM-1");

            var failure = await Record.ExceptionAsync(Startup);

            Assert.IsType<InvalidOperationException>(failure);
        }
    }
}
