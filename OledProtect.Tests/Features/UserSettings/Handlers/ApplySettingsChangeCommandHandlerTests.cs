using Moq;
using OledProtect.Features.MonitorBehavior.Commands;
using OledProtect.Features.MonitorIdleDetection.Services.Interfaces;
using OledProtect.Features.MonitorInformation.Models;
using OledProtect.Features.MonitorInformation.Services.Interfaces;
using OledProtect.Features.UserSettings.Commands;
using OledProtect.Features.UserSettings.Handlers;
using OledProtect.Features.UserSettings.Models;
using OledProtect.Messaging.Interfaces;

namespace OledProtect.Tests.Features.UserSettings.Handlers
{
    public class ApplySettingsChangeCommandHandlerTests
    {
        // Every send is logged into the shared timeline and answered with a task the test controls.
        private sealed class GatedBus(List<string> timeline) : IMediator
        {
            public TaskCompletionSource Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public bool Hold { get; set; }
            public Exception? Failure { get; set; }

            public Task SendAsync<TCommand>(TCommand command) where TCommand : ICommand
            {
                if (command is RestoreMonitorStateCommand wake)
                    timeline.Add("wake " + wake.HardwareId);
                if (Failure != null)
                    return Task.FromException(Failure);
                return Hold ? Gate.Task : Task.CompletedTask;
            }
        }

        private readonly List<string> _timeline = [];
        private readonly GatedBus _bus;
        private readonly Mock<IMonitorIdleDetectionService> _idle = new();
        private readonly Mock<IMonitorInfoManager> _screens = new();
        private readonly IReadOnlyList<MonitorInfo> _connected = [];

        public ApplySettingsChangeCommandHandlerTests()
        {
            _bus = new GatedBus(_timeline);
            _screens.Setup(s => s.GetCurrentMonitorsAsync()).ReturnsAsync(_connected);
            _idle.Setup(i => i.UpdateSettingsAsync(It.IsAny<List<MonitorSettings>>(), It.IsAny<IReadOnlyList<MonitorInfo>>()))
                .Callback(() => _timeline.Add("rules"))
                .Returns(Task.CompletedTask);
        }

        private Task Apply(params string[] ids) =>
            new ApplySettingsChangeCommandHandler(_bus, _idle.Object, _screens.Object).HandleAsync(
                new ApplySettingsChangeCommand { Settings = [.. ids.Select(id => new MonitorSettings { HardwareId = id })] });

        [Fact]
        public async Task New_rules_reach_idle_detection_before_any_screen_wakes()
        {
            await Apply("DEL01", "LG02");

            Assert.Equal(new[] { "rules", "wake DEL01", "wake LG02" }, _timeline);
        }

        [Fact]
        public async Task Idle_detection_gets_the_saved_list_and_the_connected_screens()
        {
            await Apply("DEL01");

            _idle.Verify(i => i.UpdateSettingsAsync(
                It.Is<List<MonitorSettings>>(l => l.Single().HardwareId == "DEL01"), _connected), Times.Once);
        }

        [Fact]
        public async Task The_handler_finishes_only_after_every_wake_up_has_finished()
        {
            _bus.Hold = true;

            Task handling = Apply("DEL01", "LG02");
            await Task.Delay(50);
            Assert.False(handling.IsCompleted);
            Assert.Equal(2, _timeline.Count(step => step.StartsWith("wake")));

            _bus.Gate.SetResult();
            await handling;
        }

        [Fact]
        public async Task Nobody_is_woken_when_idle_detection_rejects_the_rules()
        {
            _idle.Setup(i => i.UpdateSettingsAsync(It.IsAny<List<MonitorSettings>>(), It.IsAny<IReadOnlyList<MonitorInfo>>()))
                .ThrowsAsync(new InvalidOperationException("broken"));

            var thrown = await Record.ExceptionAsync(() => Apply("DEL01"));

            Assert.Null(thrown);
            Assert.DoesNotContain(_timeline, step => step.StartsWith("wake"));
        }

        [Fact]
        public async Task A_failing_wake_up_is_logged_instead_of_thrown()
        {
            _bus.Failure = new TimeoutException("DDC/CI did not answer");

            var thrown = await Record.ExceptionAsync(() => Apply("DEL01", "LG02"));

            Assert.Null(thrown);
            Assert.Equal(2, _timeline.Count(step => step.StartsWith("wake")));
        }
    }
}
