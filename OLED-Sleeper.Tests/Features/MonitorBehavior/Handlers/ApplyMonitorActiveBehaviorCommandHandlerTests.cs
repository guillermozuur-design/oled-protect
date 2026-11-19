using Moq;
using OLED_Sleeper.Features.MonitorBehavior.Commands;
using OLED_Sleeper.Features.MonitorBehavior.Handlers;
using OLED_Sleeper.Features.MonitorBlackout.Services.Interfaces;
using OLED_Sleeper.Features.MonitorIdleDetection.Models;
using OLED_Sleeper.Features.UserSettings.Models;
using OLED_Sleeper.Messaging.Interfaces;
using System.Windows;

namespace OLED_Sleeper.Tests.Features.MonitorBehavior.Handlers
{
    public class ApplyMonitorActiveBehaviorCommandHandlerTests
    {
        private const string Id = "SCREEN-7";
        private static readonly nint OurOverlay = 0x2A0;
        private static readonly nint SomeApp = 0x3B0;

        private readonly Mock<IMonitorBlackoutService> _overlays = new();
        private readonly Mock<IMediator> _mediator = new();

        public ApplyMonitorActiveBehaviorCommandHandlerTests()
        {
            _overlays.Setup(o => o.IsOverlayWindow(It.IsAny<nint>())).Returns((nint h) => h == OurOverlay);
        }

        private async Task<MonitorIdleStateEventArgs> Wake(ActivityReason reason, nint focused)
        {
            var wake = new MonitorIdleStateEventArgs(Id, 2, new Rect(0, 0, 2560, 1440),
                new MonitorSettings { HardwareId = Id, IsManaged = true }, focused, reason);
            await new ApplyMonitorActiveBehaviorCommandHandler(_overlays.Object, _mediator.Object)
                .HandleAsync(new ApplyMonitorActiveBehaviorCommand(wake));
            return wake;
        }

        private void VerifyRestoredOnce() =>
            _mediator.Verify(m => m.SendAsync(It.Is<RestoreMonitorStateCommand>(c => c.HardwareId == Id)), Times.Once);

        [Fact]
        public async Task FocusOnOurOwnOverlay_IsVetoedAndNothingIsRestored()
        {
            var wake = await Wake(ActivityReason.ActiveWindow, OurOverlay);

            Assert.True(wake.IsIgnored);
            _mediator.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task FocusOnAnotherWindow_RestoresTheScreen()
        {
            var wake = await Wake(ActivityReason.ActiveWindow, SomeApp);

            Assert.False(wake.IsIgnored);
            VerifyRestoredOnce();
        }

        [Theory]
        [InlineData(ActivityReason.None)]
        [InlineData(ActivityReason.MousePosition)]
        [InlineData(ActivityReason.SystemInput)]
        [InlineData(ActivityReason.KeepAwakeApp)]
        public async Task OtherReasons_RestoreEvenWhileOurOverlayHasFocus(ActivityReason reason)
        {
            var wake = await Wake(reason, OurOverlay);

            Assert.False(wake.IsIgnored);
            VerifyRestoredOnce();
        }

        [Fact]
        public async Task AFailedRestore_DoesNotEscape()
        {
            _mediator.Setup(m => m.SendAsync(It.IsAny<RestoreMonitorStateCommand>())).ThrowsAsync(new InvalidOperationException("nope"));

            var error = await Record.ExceptionAsync(() => Wake(ActivityReason.SystemInput, SomeApp));

            Assert.Null(error);
        }
    }
}
