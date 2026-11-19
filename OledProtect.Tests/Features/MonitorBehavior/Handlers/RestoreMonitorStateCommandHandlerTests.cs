using Moq;
using OledProtect.Features.MonitorBehavior.Commands;
using OledProtect.Features.MonitorBehavior.Handlers;
using OledProtect.Features.MonitorBlackout.Commands;
using OledProtect.Features.MonitorDimming.Commands;
using OledProtect.Messaging.Interfaces;

namespace OledProtect.Tests.Features.MonitorBehavior.Handlers
{
    public class RestoreMonitorStateCommandHandlerTests
    {
        private const string Id = "SCREEN-7";

        private readonly Mock<IMediator> _mediator = new();

        private Task Restore() =>
            new RestoreMonitorStateCommandHandler(_mediator.Object).HandleAsync(new RestoreMonitorStateCommand { HardwareId = Id });

        [Fact]
        public async Task RemovesTheOverlayAndUndimsThatScreenOnly()
        {
            await Restore();

            _mediator.Verify(m => m.SendAsync(It.Is<HideBlackoutOverlayCommand>(c => c.HardwareId == Id)), Times.Once);
            _mediator.Verify(m => m.SendAsync(It.Is<ApplyUndimCommand>(c => c.HardwareId == Id)), Times.Once);
            _mediator.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task OverlayFailure_DoesNotStopTheUndim()
        {
            _mediator.Setup(m => m.SendAsync(It.IsAny<HideBlackoutOverlayCommand>())).ThrowsAsync(new InvalidOperationException("stuck"));

            var error = await Record.ExceptionAsync(Restore);

            Assert.Null(error);
            _mediator.Verify(m => m.SendAsync(It.Is<ApplyUndimCommand>(c => c.HardwareId == Id)), Times.Once);
        }

        [Fact]
        public async Task UndimFailure_DoesNotEscape()
        {
            _mediator.Setup(m => m.SendAsync(It.IsAny<ApplyUndimCommand>())).ThrowsAsync(new InvalidOperationException("ddc timeout"));

            var error = await Record.ExceptionAsync(Restore);

            Assert.Null(error);
            _mediator.Verify(m => m.SendAsync(It.Is<HideBlackoutOverlayCommand>(c => c.HardwareId == Id)), Times.Once);
        }

        [Fact]
        public void ACommandWithoutAScreen_IsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new RestoreMonitorStateCommand { HardwareId = null! });
        }
    }
}
