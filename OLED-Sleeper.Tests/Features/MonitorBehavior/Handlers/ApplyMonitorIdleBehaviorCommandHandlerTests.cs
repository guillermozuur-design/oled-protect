using Moq;
using OLED_Sleeper.Features.MonitorBehavior.Commands;
using OLED_Sleeper.Features.MonitorBehavior.Handlers;
using OLED_Sleeper.Features.MonitorBehavior.Models;
using OLED_Sleeper.Features.MonitorBlackout.Commands;
using OLED_Sleeper.Features.MonitorBlackout.Models;
using OLED_Sleeper.Features.MonitorDimming.Commands;
using OLED_Sleeper.Features.MonitorIdleDetection.Models;
using OLED_Sleeper.Features.UserSettings.Models;
using OLED_Sleeper.Messaging.Interfaces;
using System.Windows;

namespace OLED_Sleeper.Tests.Features.MonitorBehavior.Handlers
{
    public class ApplyMonitorIdleBehaviorCommandHandlerTests
    {
        private const string Id = "SCREEN-7";

        private readonly Mock<IMediator> _mediator = new();

        private Task GoIdle(MonitorBehaviorType kind, double dimLevel = 15, bool lowerBrightness = false)
        {
            var config = new MonitorSettings
            {
                HardwareId = Id,
                Behavior = kind,
                DimLevel = dimLevel,
                LowerBrightnessOnBlackout = lowerBrightness
            };
            var idle = new MonitorIdleStateEventArgs(Id, 1, new Rect(0, 0, 1920, 1080), config, nint.Zero, ActivityReason.None);
            return new ApplyMonitorIdleBehaviorCommandHandler(_mediator.Object).HandleAsync(new ApplyMonitorIdleBehaviorCommand(idle));
        }

        [Theory]
        [InlineData(MonitorBehaviorType.Blackout, OverlayScene.Black)]
        [InlineData(MonitorBehaviorType.Screensaver, OverlayScene.Stars)]
        [InlineData(MonitorBehaviorType.Flights, OverlayScene.Flights)]
        public async Task OverlayBehaviours_SendTheMatchingScene(MonitorBehaviorType behavior, OverlayScene scene)
        {
            await GoIdle(behavior);

            _mediator.Verify(m => m.SendAsync(It.Is<ApplyBlackoutOverlayCommand>(c => c.HardwareId == Id && c.Scene == scene)), Times.Once);
            _mediator.Verify(m => m.SendAsync(It.IsAny<ApplyDimCommand>()), Times.Never);
        }

        [Theory]
        [InlineData(MonitorBehaviorType.Blackout, true, true)]
        [InlineData(MonitorBehaviorType.Blackout, false, false)]
        [InlineData(MonitorBehaviorType.Screensaver, true, false)]
        [InlineData(MonitorBehaviorType.Flights, true, false)]
        public async Task OnlyPlainBlackout_PassesOnLowerBrightness(MonitorBehaviorType behavior, bool setting, bool expected)
        {
            await GoIdle(behavior, lowerBrightness: setting);

            _mediator.Verify(m => m.SendAsync(It.Is<ApplyBlackoutOverlayCommand>(c => c.LowerBrightness == expected)), Times.Once);
        }

        [Theory]
        [InlineData(20, 20)]
        [InlineData(42.9, 42)]
        public async Task Dim_SendsTheLevelRoundedTowardZero(double level, int expected)
        {
            await GoIdle(MonitorBehaviorType.Dim, dimLevel: level);

            _mediator.Verify(m => m.SendAsync(It.Is<ApplyDimCommand>(c => c.HardwareId == Id && c.DimLevel == expected)), Times.Once);
            _mediator.Verify(m => m.SendAsync(It.IsAny<ApplyBlackoutOverlayCommand>()), Times.Never);
        }

        [Fact]
        public async Task None_DoesNothing()
        {
            await GoIdle(MonitorBehaviorType.None);

            _mediator.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task AFailingFollowUpCommand_ReachesTheCaller()
        {
            _mediator.Setup(m => m.SendAsync(It.IsAny<ApplyBlackoutOverlayCommand>())).ThrowsAsync(new InvalidOperationException("unregistered"));

            var error = await Record.ExceptionAsync(() => GoIdle(MonitorBehaviorType.Blackout));

            Assert.IsType<InvalidOperationException>(error);
        }
    }
}
