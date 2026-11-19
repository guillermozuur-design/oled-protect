using Moq;
using OledProtect.Features.MonitorDimming.Commands;
using OledProtect.Features.MonitorDimming.Handlers;
using OledProtect.Features.MonitorDimming.Services.Interfaces;

namespace OledProtect.Tests.Features.MonitorDimming.Handlers
{
    public sealed class RestoreBrightnessOnAllMonitorsCommandHandlerTests
    {
        private readonly Mock<IMonitorDimmingService> _dimming = new();

        private Task Run() =>
            new RestoreBrightnessOnAllMonitorsCommandHandler(_dimming.Object).HandleAsync(new RestoreBrightnessOnAllMonitorsCommand());

        private void LeftDimmed(params string[] hardwareIds) =>
            _dimming.Setup(d => d.GetDimmedMonitors()).Returns(hardwareIds.ToDictionary(id => id, _ => 80u));

        [Fact]
        public async Task NothingLeftDimmed_RestoresNothing()
        {
            LeftDimmed();

            await Run();

            _dimming.Verify(d => d.UndimMonitorAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task EveryMonitorLeftDimmed_IsRestoredOnce()
        {
            LeftDimmed("PNP-LEFT", "PNP-RIGHT");

            await Run();

            _dimming.Verify(d => d.UndimMonitorAsync("PNP-LEFT"), Times.Once);
            _dimming.Verify(d => d.UndimMonitorAsync("PNP-RIGHT"), Times.Once);
        }

        [Fact]
        public async Task FailureInTheDimmingService_ReachesTheCaller()
        {
            LeftDimmed("PNP-LEFT");
            _dimming.Setup(d => d.UndimMonitorAsync("PNP-LEFT")).ThrowsAsync(new InvalidOperationException("bus error"));

            await Assert.ThrowsAsync<InvalidOperationException>(Run);
        }
    }
}
