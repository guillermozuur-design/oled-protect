using OledProtect.Features.MonitorBehavior.Commands;
using OledProtect.Features.MonitorBehavior.Handlers;
using OledProtect.Features.MonitorIdleDetection.Models;
using OledProtect.Features.UserSettings.Models;
using OledProtect.Tests.TestDoubles;
using System.Windows;

namespace OledProtect.Tests.Features.MonitorBehavior.Handlers
{
    public class ApplyMonitorActiveBehaviorCommandHandlerTests
    {
        private const string Panel = "SAM-ODYSSEY";
        private const nint Overlay = 0x51C0;
        private const nint Browser = 0x51C4;

        private readonly RecordingBlackoutService _blackout = new() { OwnHandles = { Overlay } };
        private readonly RecordingMediator _mediator = new();

        private static MonitorIdleStateEventArgs Activity(ActivityReason reason, nint focus) =>
            new(Panel, 3, new Rect(0, 0, 3440, 1440), new MonitorSettings { HardwareId = Panel, IsManaged = true }, focus, reason);

        private Task Handle(MonitorIdleStateEventArgs activity) =>
            new ApplyMonitorActiveBehaviorCommandHandler(_blackout, _mediator).HandleAsync(new ApplyMonitorActiveBehaviorCommand(activity));

        private string[] RestoredScreens => _mediator.SentOf<RestoreMonitorStateCommand>().Select(c => c.HardwareId).ToArray();

        [Fact]
        public async Task Our_overlay_taking_focus_is_vetoed_and_restores_nothing()
        {
            var activity = Activity(ActivityReason.ActiveWindow, Overlay);

            await Handle(activity);

            Assert.True(activity.IsIgnored);
            Assert.Empty(_mediator.Sent);
        }

        [Fact]
        public async Task Another_window_taking_focus_wakes_the_screen()
        {
            var activity = Activity(ActivityReason.ActiveWindow, Browser);

            await Handle(activity);

            Assert.False(activity.IsIgnored);
            Assert.Equal([Panel], RestoredScreens);
        }

        public static IEnumerable<object[]> NonFocusReasons =>
            Enum.GetValues<ActivityReason>().Where(r => r != ActivityReason.ActiveWindow).Select(r => new object[] { r });

        [Theory]
        [MemberData(nameof(NonFocusReasons))]
        public async Task Other_activity_wakes_the_screen_whatever_has_focus(ActivityReason reason)
        {
            var activity = Activity(reason, Overlay);

            await Handle(activity);

            Assert.False(activity.IsIgnored);
            Assert.Equal([Panel], RestoredScreens);
        }

        [Fact]
        public async Task A_failing_restore_is_contained()
        {
            _mediator.Failing<RestoreMonitorStateCommand>(new InvalidOperationException("handler missing"));
            var activity = Activity(ActivityReason.SystemInput, Browser);

            await Handle(activity);

            Assert.Equal([Panel], RestoredScreens);
        }
    }
}
