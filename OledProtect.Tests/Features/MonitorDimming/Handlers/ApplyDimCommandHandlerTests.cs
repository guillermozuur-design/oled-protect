using Moq;
using OledProtect.Features.MonitorDimming.Commands;
using OledProtect.Features.MonitorDimming.Handlers;
using OledProtect.Features.MonitorDimming.Services.Interfaces;

namespace OledProtect.Tests.Features.MonitorDimming.Handlers
{
    public sealed class ApplyDimCommandHandlerTests
    {
        private readonly Mock<IMonitorDimmingService> _dimming = new();
        private readonly ApplyDimCommand _dimTo15 = new() { HardwareId = "PNP-OLED", DimLevel = 15 };

        [Fact]
        public async Task PassesTheMonitorAndLevelToTheDimmingService()
        {
            await new ApplyDimCommandHandler(_dimming.Object).HandleAsync(_dimTo15);

            _dimming.Verify(d => d.DimMonitorAsync("PNP-OLED", 15), Times.Once);
        }

        [Fact]
        public async Task FailureInTheDimmingService_IsLoggedNotThrown()
        {
            _dimming.Setup(d => d.DimMonitorAsync("PNP-OLED", 15)).ThrowsAsync(new InvalidOperationException("bus error"));

            var thrown = await Record.ExceptionAsync(() => new ApplyDimCommandHandler(_dimming.Object).HandleAsync(_dimTo15));

            Assert.Null(thrown);
            _dimming.Verify(d => d.DimMonitorAsync("PNP-OLED", 15), Times.Once);
        }
    }
}
