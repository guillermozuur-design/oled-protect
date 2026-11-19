using OledProtect.Features.MonitorBehavior.Commands;
using OledProtect.Features.MonitorBehavior.Handlers;
using OledProtect.Features.MonitorBehavior.Models;
using OledProtect.Features.MonitorBlackout.Commands;
using OledProtect.Features.MonitorBlackout.Models;
using OledProtect.Features.MonitorDimming.Commands;
using OledProtect.Features.MonitorIdleDetection.Models;
using OledProtect.Features.UserSettings.Models;
using OledProtect.Tests.TestDoubles;
using System.Windows;

namespace OledProtect.Tests.Features.MonitorBehavior.Handlers
{
    public class ApplyMonitorIdleBehaviorCommandHandlerTests
    {
        private const string Panel = "ASUS-PG32";

        private readonly RecordingMediator _mediator = new();

        private Task Idle(MonitorSettings settings)
        {
            settings.HardwareId = Panel;
            var snapshot = new MonitorIdleStateEventArgs(Panel, 2, new Rect(-3840, 0, 3840, 2160), settings, 0, ActivityReason.None);
            return new ApplyMonitorIdleBehaviorCommandHandler(_mediator).HandleAsync(new ApplyMonitorIdleBehaviorCommand(snapshot));
        }

        private ApplyBlackoutOverlayCommand OnlyCover() => Assert.Single(_mediator.SentOf<ApplyBlackoutOverlayCommand>());

        [Theory]
        [InlineData(MonitorBehaviorType.Blackout, OverlayScene.Black)]
        [InlineData(MonitorBehaviorType.Screensaver, OverlayScene.Stars)]
        [InlineData(MonitorBehaviorType.Flights, OverlayScene.Flights)]
        public async Task Cover_behaviours_ask_for_their_own_scene(MonitorBehaviorType behaviour, OverlayScene scene)
        {
            await Idle(new MonitorSettings { Behavior = behaviour });

            ApplyBlackoutOverlayCommand cover = OnlyCover();
            Assert.Equal((Panel, scene), (cover.HardwareId, cover.Scene));
            Assert.Empty(_mediator.SentOf<ApplyDimCommand>());
        }

        [Theory]
        [InlineData(MonitorBehaviorType.Blackout, false, false)]
        [InlineData(MonitorBehaviorType.Blackout, true, true)]
        [InlineData(MonitorBehaviorType.Screensaver, true, false)]
        [InlineData(MonitorBehaviorType.Flights, true, false)]
        public async Task Only_plain_black_may_switch_the_backlight_down(MonitorBehaviorType behaviour, bool userWantsIt, bool expected)
        {
            await Idle(new MonitorSettings { Behavior = behaviour, LowerBrightnessOnBlackout = userWantsIt });

            Assert.Equal(expected, OnlyCover().LowerBrightness);
        }

        [Theory]
        [InlineData(35.0, 35)]
        [InlineData(7.99, 7)]
        [InlineData(0.4, 0)]
        public async Task Dimming_passes_the_level_with_the_fraction_cut_off(double configured, int sent)
        {
            await Idle(new MonitorSettings { Behavior = MonitorBehaviorType.Dim, DimLevel = configured });

            ApplyDimCommand dim = Assert.Single(_mediator.SentOf<ApplyDimCommand>());
            Assert.Equal((Panel, sent), (dim.HardwareId, dim.DimLevel));
            Assert.Single(_mediator.Sent);
        }

        [Fact]
        public async Task No_behaviour_sends_nothing()
        {
            await Idle(new MonitorSettings { Behavior = MonitorBehaviorType.None });

            Assert.Empty(_mediator.Sent);
        }

        [Fact]
        public async Task A_failing_follow_up_command_is_passed_on()
        {
            _mediator.Failing<ApplyDimCommand>(new TimeoutException("ddc"));

            await Assert.ThrowsAsync<TimeoutException>(() => Idle(new MonitorSettings { Behavior = MonitorBehaviorType.Dim }));
        }
    }
}
