using Moq;
using OLED_Sleeper.Features.MonitorDimming.Commands;
using OLED_Sleeper.Features.MonitorDimming.Handlers;
using OLED_Sleeper.Features.MonitorDimming.Services.Interfaces;

namespace OLED_Sleeper.Tests.Features.MonitorDimming.Handlers
{
    public sealed class ApplyUndimCommandHandlerTests
    {
        private readonly Mock<IMonitorDimmingService> _dimming = new();
        private readonly ApplyUndimCommand _undim = new() { HardwareId = "PNP-OLED" };

        [Fact]
        public async Task AsksTheDimmingServiceToRestoreThatMonitor()
        {
            await new ApplyUndimCommandHandler(_dimming.Object).HandleAsync(_undim);

            _dimming.Verify(d => d.UndimMonitorAsync("PNP-OLED"), Times.Once);
        }

        [Fact]
        public async Task FailureInTheDimmingService_IsLoggedNotThrown()
        {
            _dimming.Setup(d => d.UndimMonitorAsync("PNP-OLED")).ThrowsAsync(new InvalidOperationException("bus error"));

            var thrown = await Record.ExceptionAsync(() => new ApplyUndimCommandHandler(_dimming.Object).HandleAsync(_undim));

            Assert.Null(thrown);
            _dimming.Verify(d => d.UndimMonitorAsync("PNP-OLED"), Times.Once);
        }
    }
}
