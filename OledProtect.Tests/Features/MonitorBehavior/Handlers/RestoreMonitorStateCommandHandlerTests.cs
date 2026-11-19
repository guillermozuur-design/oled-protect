using OledProtect.Features.MonitorBehavior.Commands;
using OledProtect.Features.MonitorBehavior.Handlers;
using OledProtect.Features.MonitorBlackout.Commands;
using OledProtect.Features.MonitorDimming.Commands;
using OledProtect.Messaging.Interfaces;
using OledProtect.Tests.TestDoubles;

namespace OledProtect.Tests.Features.MonitorBehavior.Handlers
{
    public class RestoreMonitorStateCommandHandlerTests
    {
        private const string Panel = "BENQ-MOBIUZ";

        private readonly RecordingMediator _mediator = new();

        private Task Wake() =>
            new RestoreMonitorStateCommandHandler(_mediator).HandleAsync(new RestoreMonitorStateCommand { HardwareId = Panel });

        // Each sent command reduced to "<kind>:<screen>", sorted, since both are sent side by side.
        private string[] Sent => _mediator.Sent
            .Select(c => c switch
            {
                HideBlackoutOverlayCommand hide => "uncover:" + hide.HardwareId,
                ApplyUndimCommand undim => "undim:" + undim.HardwareId,
                ICommand other => "unexpected:" + other.GetType().Name,
            })
            .Order()
            .ToArray();

        [Fact]
        public async Task Uncovers_and_undims_only_the_given_screen()
        {
            await Wake();

            Assert.Equal(["uncover:" + Panel, "undim:" + Panel], Sent);
        }

        [Fact]
        public async Task A_stuck_overlay_does_not_block_the_undim()
        {
            _mediator.Failing<HideBlackoutOverlayCommand>(new InvalidOperationException("window hung"));

            await Wake();

            Assert.Contains("undim:" + Panel, Sent);
        }

        [Fact]
        public async Task A_failed_undim_does_not_block_the_uncover()
        {
            _mediator.Failing<ApplyUndimCommand>(new TimeoutException("ddc"));

            await Wake();

            Assert.Contains("uncover:" + Panel, Sent);
        }

        [Fact]
        public async Task Both_steps_failing_is_still_quiet()
        {
            _mediator.Failing<HideBlackoutOverlayCommand>(new InvalidOperationException("a"))
                     .Failing<ApplyUndimCommand>(new InvalidOperationException("b"));

            Exception? escaped = await Record.ExceptionAsync(Wake);

            Assert.Null(escaped);
            Assert.Equal(2, Sent.Length);
        }

        [Fact]
        public void A_command_needs_a_screen()
        {
            Assert.Throws<ArgumentNullException>(() => new RestoreMonitorStateCommand { HardwareId = null! });
        }
    }
}
