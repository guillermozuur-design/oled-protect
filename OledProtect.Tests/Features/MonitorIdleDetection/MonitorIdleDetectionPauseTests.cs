using Moq;
using OledProtect.Features.MonitorBehavior.Commands;
using OledProtect.Features.MonitorIdleDetection.Services;
using OledProtect.Features.MonitorInformation.Models;
using OledProtect.Features.UserSettings.Models;
using OledProtect.Messaging.Interfaces;

namespace OledProtect.Tests.Features.MonitorIdleDetection
{
    public class MonitorIdleDetectionPauseTests
    {
        [Fact]
        public async Task SetPaused_True_WakesEveryManagedMonitorOnce()
        {
            var mediator = new Mock<IMediator>();
            var service = new MonitorIdleDetectionService(mediator.Object);
            await service.UpdateSettingsAsync(
                new List<MonitorSettings>
                {
                    new() { HardwareId = "A", IsManaged = true },
                    new() { HardwareId = "B", IsManaged = true },
                    new() { HardwareId = "C", IsManaged = false }
                },
                new List<MonitorInfo>
                {
                    new() { HardwareId = "A", DisplayNumber = 1 },
                    new() { HardwareId = "B", DisplayNumber = 2 },
                    new() { HardwareId = "C", DisplayNumber = 3 }
                });

            service.SetPaused(true);
            service.SetPaused(true); // tweede keer doet niets

            mediator.Verify(m => m.SendAsync(It.Is<RestoreMonitorStateCommand>(c => c.HardwareId == "A")), Times.Once);
            mediator.Verify(m => m.SendAsync(It.Is<RestoreMonitorStateCommand>(c => c.HardwareId == "B")), Times.Once);
            mediator.Verify(m => m.SendAsync(It.Is<RestoreMonitorStateCommand>(c => c.HardwareId == "C")), Times.Never);
        }

        [Fact]
        public async Task SetPaused_False_SendsNothing()
        {
            var mediator = new Mock<IMediator>();
            var service = new MonitorIdleDetectionService(mediator.Object);
            await service.UpdateSettingsAsync(
                new List<MonitorSettings> { new() { HardwareId = "A", IsManaged = true } },
                new List<MonitorInfo> { new() { HardwareId = "A" } });

            service.SetPaused(true);
            mediator.Invocations.Clear();
            service.SetPaused(false);

            Assert.Empty(mediator.Invocations);
        }
    }
}
