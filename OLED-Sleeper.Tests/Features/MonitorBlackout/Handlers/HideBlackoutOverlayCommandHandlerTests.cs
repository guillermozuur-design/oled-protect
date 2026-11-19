using Moq;
using OLED_Sleeper.Features.MonitorBlackout.Commands;
using OLED_Sleeper.Features.MonitorBlackout.Handlers;
using OLED_Sleeper.Features.MonitorBlackout.Services.Interfaces;

namespace OLED_Sleeper.Tests.Features.MonitorBlackout.Handlers
{
    public class HideBlackoutOverlayCommandHandlerTests
    {
        private readonly Mock<IMonitorBlackoutService> _overlays = new();

        private Task Run(string id) =>
            new HideBlackoutOverlayCommandHandler(_overlays.Object).HandleAsync(new HideBlackoutOverlayCommand { HardwareId = id });

        [Theory]
        [InlineData("SCREEN-7")]
        [InlineData("")]
        public async Task Hides_TheOverlayOfTheGivenScreen(string id)
        {
            await Run(id);

            _overlays.Verify(o => o.HideBlackoutOverlayAsync(id), Times.Once);
            _overlays.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task AFailingOverlay_IsSwallowed()
        {
            _overlays.Setup(o => o.HideBlackoutOverlayAsync("SCREEN-7")).ThrowsAsync(new InvalidOperationException("boom"));

            Exception? error = await Record.ExceptionAsync(() => Run("SCREEN-7"));

            Assert.Null(error);
            _overlays.Verify(o => o.HideBlackoutOverlayAsync("SCREEN-7"), Times.Once);
        }
    }
}
