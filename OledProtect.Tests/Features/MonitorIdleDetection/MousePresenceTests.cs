using OledProtect.Features.MonitorIdleDetection.Services;
using OledProtect.Features.UserSettings.Models;

namespace OledProtect.Tests.Features.MonitorIdleDetection
{
    public class MousePresenceTests
    {
        private static MonitorSettings Settings(bool limit, int minutes = 10) =>
            new() { IsActiveOnMousePosition = true, UseMouseStillLimit = limit, MouseStillMinutes = minutes };

        [Fact]
        public void WithoutLimit_AParkedMouseKeepsTheMonitorAwakeForever()
        {
            Assert.True(MonitorIdleDetectionService.MousePresenceCounts(Settings(false), true, uint.MaxValue));
        }

        [Fact]
        public void WithLimit_AParkedMouseCountsUntilTheLimit()
        {
            var settings = Settings(true, 10);
            Assert.True(MonitorIdleDetectionService.MousePresenceCounts(settings, true, 9 * 60_000));
            Assert.False(MonitorIdleDetectionService.MousePresenceCounts(settings, true, 10 * 60_000));
        }

        [Fact]
        public void StillLimit_WhenOff_NeverStopsActivity_WhenOn_StopsAfterTheLimit()
        {
            Assert.True(MonitorIdleDetectionService.WithinStillLimit(Settings(false), uint.MaxValue));
            Assert.True(MonitorIdleDetectionService.WithinStillLimit(Settings(true, 5), 4 * 60_000));
            Assert.False(MonitorIdleDetectionService.WithinStillLimit(Settings(true, 5), 5 * 60_000));
        }

        [Fact]
        public void MouseOnAnotherMonitor_NeverCounts()
        {
            Assert.False(MonitorIdleDetectionService.MousePresenceCounts(Settings(false), false, 0));
        }

        [Fact]
        public void MouseConditionSwitchedOff_NeverCounts()
        {
            var settings = Settings(false);
            settings.IsActiveOnMousePosition = false;
            Assert.False(MonitorIdleDetectionService.MousePresenceCounts(settings, true, 0));
        }
    }
}
