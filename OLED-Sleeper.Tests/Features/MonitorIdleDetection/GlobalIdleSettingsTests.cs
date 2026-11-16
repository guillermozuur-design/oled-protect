using OLED_Sleeper.Features.MonitorIdleDetection.Services;

namespace OLED_Sleeper.Tests.Features.MonitorIdleDetection
{
    public class GlobalIdleSettingsTests
    {
        [Fact]
        public void Off_NeverExpires()
        {
            var settings = new GlobalIdleSettings();
            Assert.False(settings.HasExpired(uint.MaxValue));
        }

        [Fact]
        public void On_ExpiresAfterTheMinutes()
        {
            var settings = new GlobalIdleSettings();
            settings.Set(true, 3);
            Assert.False(settings.HasExpired(3 * 60_000 - 1));
            Assert.True(settings.HasExpired(3 * 60_000));
        }

        [Fact]
        public void Minutes_AreClamped()
        {
            var settings = new GlobalIdleSettings();
            settings.Set(true, 0);
            Assert.Equal(1, settings.Minutes);
        }
    }
}
