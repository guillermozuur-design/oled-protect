using OledProtect.Features.MonitorIdleDetection.Services;
using OledProtect.Features.UserSettings.Models;

namespace OledProtect.Tests.Features.MonitorIdleDetection
{
    public class MousePresenceTests
    {
        [Fact]
        public void AParkedMouseKeepsItsMonitorAwake()
        {
            Assert.True(MonitorIdleDetectionService.MousePresenceCounts(new MonitorSettings { IsActiveOnMousePosition = true }, true));
        }

        [Fact]
        public void MouseOnAnotherMonitor_NeverCounts()
        {
            Assert.False(MonitorIdleDetectionService.MousePresenceCounts(new MonitorSettings { IsActiveOnMousePosition = true }, false));
        }

        [Fact]
        public void MouseConditionSwitchedOff_NeverCounts()
        {
            Assert.False(MonitorIdleDetectionService.MousePresenceCounts(new MonitorSettings { IsActiveOnMousePosition = false }, true));
        }
    }
}
