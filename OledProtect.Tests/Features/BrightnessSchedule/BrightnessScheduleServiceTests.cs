using Moq;
using OledProtect.Features.BrightnessSchedule.Services;
using OledProtect.Features.MonitorDimming.Services.Interfaces;
using OledProtect.Features.MonitorInformation.Models;
using OledProtect.Features.MonitorInformation.Services.Interfaces;
using OledProtect.Features.UserSettings.Models;
using OledProtect.Features.UserSettings.Services.Interfaces;

namespace OledProtect.Tests.Features.BrightnessSchedule
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

            // The per-screen schedule is taken over into the one schedule for all screens.
            var schedule = new GlobalBrightnessSchedule(null, settings.Object);
            var service = new BrightnessScheduleService(settings.Object, monitors.Object, dimming.Object, schedule);
            service.Reapply();
            for (int i = 0; i < 40 && dimming.Invocations.Count < 1; i++) await Task.Delay(25);
            await Task.Delay(50);
            service.Reapply(); // stands in for the next 30-second check
            for (int i = 0; i < 40 && dimming.Invocations.Count < 2; i++) await Task.Delay(25);

            dimming.Verify(d => d.SetScheduledBrightnessAsync("A", 40), Times.Exactly(2));
        }

        [Fact]
        public void GlobalSchedule_TakesOverTheFirstScreenScheduleThatWasOn()
        {
            var screens = new List<MonitorSettings>
            {
                new() { HardwareId = "A", UseBrightnessSchedule = false },
                new() { HardwareId = "B", UseBrightnessSchedule = true, BrightnessSchedule = new() { new() { Start = "07:00", Brightness = 90 }, new() { Start = "22:00", Brightness = 30 } } },
            };
            var taken = GlobalBrightnessSchedule.FromScreens(screens);
            Assert.True(taken.Enabled);
            Assert.Equal(new[] { "07:00", "22:00" }, taken.Entries.Select(e => e.Start));

            var none = GlobalBrightnessSchedule.FromScreens(new List<MonitorSettings> { new() });
            Assert.False(none.Enabled);
            Assert.NotEmpty(none.Entries); // the default steps, ready to switch on
        }

        [Fact]
        public void GlobalSchedule_TargetAndChanges()
        {
            var schedule = new GlobalBrightnessSchedule();
            int changes = 0;
            schedule.Changed += () => changes++;
            schedule.Set(true, new[] { new BrightnessScheduleEntry { Start = "08:00", Brightness = 80 }, new BrightnessScheduleEntry { Start = "20:00", Brightness = 40 },
                                       new BrightnessScheduleEntry { Start = "nonsense", Brightness = 10 } });
            Assert.Equal(80, schedule.TargetAt(new TimeOnly(12, 0)));
            Assert.Equal(40, schedule.TargetAt(new TimeOnly(2, 0)));   // after the last step until the first one
            Assert.Equal(2, schedule.Entries.Count);                    // the broken time is left out
            schedule.Set(true, schedule.Entries);                       // nothing changed: no event
            schedule.Set(false, schedule.Entries);
            Assert.Null(schedule.TargetAt(new TimeOnly(12, 0)));
            Assert.Equal(2, changes);
        }
    }
}
