using Moq;
using OLED_Sleeper.Features.BrightnessSchedule.Services;
using OLED_Sleeper.Features.MonitorDimming.Services.Interfaces;
using OLED_Sleeper.Features.MonitorInformation.Models;
using OLED_Sleeper.Features.MonitorInformation.Services.Interfaces;
using OLED_Sleeper.Features.UserSettings.Models;
using OLED_Sleeper.Features.UserSettings.Services.Interfaces;

namespace OLED_Sleeper.Tests.Features.BrightnessSchedule
{
    public class BrightnessScheduleServiceTests
    {
        [Fact]
        public async Task AFailedWrite_IsRetriedOnTheNextCheck()
        {
            var settings = new Mock<IMonitorSettingsFileService>();
            settings.Setup(s => s.LoadSettings()).Returns(new List<MonitorSettings>
            {
                new() { HardwareId = "A", UseBrightnessSchedule = true,
                        BrightnessSchedule = new() { new() { Start = "00:00", Brightness = 40 } } }
            });
            var monitors = new Mock<IMonitorInfoManager>();
            monitors.Setup(m => m.GetCurrentMonitorsAsync()).ReturnsAsync(new[]
            {
                new MonitorInfo { HardwareId = "A", Capabilities = new DdcCiCapabilities(true, 100) }
            });
            var dimming = new Mock<IMonitorDimmingService>();
            dimming.SetupSequence(d => d.SetScheduledBrightnessAsync("A", 40)).ReturnsAsync(false).ReturnsAsync(true);

            var service = new BrightnessScheduleService(settings.Object, monitors.Object, dimming.Object);
            service.Reapply();
            for (int i = 0; i < 40 && dimming.Invocations.Count < 1; i++) await Task.Delay(25);
            await Task.Delay(50);
            service.Reapply(); // stands in for the next 30-second check
            for (int i = 0; i < 40 && dimming.Invocations.Count < 2; i++) await Task.Delay(25);

            dimming.Verify(d => d.SetScheduledBrightnessAsync("A", 40), Times.Exactly(2));
        }
    }
}
