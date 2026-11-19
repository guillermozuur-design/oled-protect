using OledProtect.Features.MonitorDimming.Helpers;

namespace OledProtect.Tests.Features.MonitorDimming.Helpers
{
    public sealed class BrightnessScaleTests
    {
        // percent, panel maximum, expected raw level
        public static TheoryData<int, uint, uint> Table => new()
        {
            { 0, 255, 0 },
            { 1, 255, 3 },              // 2.55
            { 15, 255, 38 },            // 38.25
            { 50, 255, 128 },           // 127.5, half rounds up
            { 60, 255, 153 },
            { 99, 255, 252 },           // 252.45
            { 100, 255, 255 },
            { 50, 50, 25 },
            { 33, 10, 3 },              // 3.3
            { 35, 10, 4 },              // 3.5, half rounds up
            { 15, 0, 15 },              // no range reported: percent as is
            { 15, 100, 15 },            // the panel already works in percent
            { -1, 255, 0 },             // clamped to 0 first
            { 101, 255, 255 },          // clamped to 100 first
            { 250, 0, 100 },            // clamped even without a range
            { 100, uint.MaxValue, uint.MaxValue }, // no overflow on huge ranges
        };

        [Theory]
        [MemberData(nameof(Table))]
        public void Percent_maps_onto_the_panel_range(int percent, uint maximum, uint expected) =>
            Assert.Equal(expected, BrightnessScale.ToRawBrightness(percent, maximum));
    }
}
