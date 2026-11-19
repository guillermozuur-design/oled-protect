using OledProtect.Features.MonitorBlackout.Commands;
using OledProtect.Features.MonitorBlackout.Handlers;
using OledProtect.Tests.TestDoubles;
using Request = OledProtect.Tests.TestDoubles.RecordingBlackoutService.Request;

namespace OledProtect.Tests.Features.MonitorBlackout.Handlers
{
    public class HideBlackoutOverlayCommandHandlerTests
    {
        private readonly RecordingBlackoutService _blackout = new();

        private Task Handle(string hardwareId) =>
            new HideBlackoutOverlayCommandHandler(_blackout).HandleAsync(new HideBlackoutOverlayCommand { HardwareId = hardwareId });

        [Theory]
        [InlineData("DEL-01")]
        [InlineData("")]
        public async Task Asks_the_service_to_uncover_exactly_that_screen(string hardwareId)
        {
            await Handle(hardwareId);

            Assert.Equal([new Request("hide", hardwareId)], _blackout.Requests);
        }

        [Fact]
        public async Task A_service_failure_is_logged_not_thrown()
        {
            _blackout.FailWith = new InvalidOperationException("window already gone");

            await Handle("DEL-01");

            Assert.Single(_blackout.Requests);
        }
    }
}
