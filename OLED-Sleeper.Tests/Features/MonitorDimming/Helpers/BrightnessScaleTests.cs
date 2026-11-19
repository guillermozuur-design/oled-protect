using OLED_Sleeper.Features.MonitorDimming.Helpers;

namespace OLED_Sleeper.Tests.Features.MonitorDimming.Helpers
{
    public sealed class BrightnessScaleTests
    {
        [Theory]
        [InlineData(1, 3u)]     // 2.55
        [InlineData(15, 38u)]   // 38.25
        [InlineData(50, 128u)]  // 127.5: halves round up
        [InlineData(60, 153u)]
        [InlineData(99, 252u)]  // 252.45
        public void PanelWithItsOwnRange_GetsThePercentOfThatRange(int percent, uint expected)
        {
            Assert.Equal(expected, BrightnessScale.ToRawBrightness(percent, 255));
        }

        [Theory]
        [InlineData(0u)]   // the panel never reported a range
        [InlineData(100u)] // the panel already counts in percent
        public void PanelWithoutAUsableRange_GetsThePercentUnchanged(uint maxBrightness)
        {
            Assert.Equal(15u, BrightnessScale.ToRawBrightness(15, maxBrightness));
        }

        [Theory]
        [InlineData(-1, 0u)]
        [InlineData(101, 255u)]
        public void PercentOutsideZeroToHundred_IsClampedFirst(int percent, uint expected)
        {
            Assert.Equal(expected, BrightnessScale.ToRawBrightness(percent, 255));
        }

        [Fact]
        public void TheEndsOfThePercentScale_MapToTheEndsOfTheRange()
        {
            Assert.Equal(0u, BrightnessScale.ToRawBrightness(0, 255));
            Assert.Equal(255u, BrightnessScale.ToRawBrightness(100, 255));
        }

        [Fact]
        public void VeryLargeRange_DoesNotOverflow()
        {
            Assert.Equal(uint.MaxValue, BrightnessScale.ToRawBrightness(100, uint.MaxValue));
        }
    }
}
