using OLED_Sleeper.Features.BrightnessSchedule.Services;
using OLED_Sleeper.Features.UserSettings.Models;

namespace OLED_Sleeper.Tests.Features.BrightnessSchedule
{
    public class BrightnessScheduleCalculatorTests
    {
        private static readonly List<BrightnessScheduleEntry> Schedule = new()
        {
            new() { Start = "19:00", Brightness = 50 },   // bewust niet op volgorde
            new() { Start = "08:00", Brightness = 80 },
            new() { Start = "23:00", Brightness = 30 }
        };

        [Theory]
        [InlineData(8, 0, 80)]
        [InlineData(12, 30, 80)]
        [InlineData(18, 59, 80)]
        [InlineData(19, 0, 50)]
        [InlineData(23, 15, 30)]
        [InlineData(2, 0, 30)]   // na middernacht geldt de laatste van de vorige dag nog
        [InlineData(7, 59, 30)]
        public void GetTargetBrightness_PicksTheEntryInForce(int hour, int minute, int expected)
        {
            Assert.Equal(expected, BrightnessScheduleCalculator.GetTargetBrightness(Schedule, new TimeOnly(hour, minute)));
        }

        [Fact]
        public void GetTargetBrightness_IgnoresInvalidTimesAndClampsBrightness()
        {
            var schedule = new List<BrightnessScheduleEntry>
            {
                new() { Start = "kwart over", Brightness = 10 },
                new() { Start = "9:00", Brightness = 150 }
            };

            Assert.Equal(100, BrightnessScheduleCalculator.GetTargetBrightness(schedule, new TimeOnly(10, 0)));
        }

        [Fact]
        public void GetTargetBrightness_WithoutValidEntries_ReturnsNull()
        {
            Assert.Null(BrightnessScheduleCalculator.GetTargetBrightness(new List<BrightnessScheduleEntry>(), new TimeOnly(10, 0)));
            Assert.Null(BrightnessScheduleCalculator.GetTargetBrightness(null, new TimeOnly(10, 0)));
        }

        [Theory]
        [InlineData("08:00", true)]
        [InlineData("8:05", true)]
        [InlineData(" 23:59 ", true)]
        [InlineData("24:00", false)]
        [InlineData("8.00", false)]
        [InlineData("", false)]
        public void ParseTime_AcceptsHoursAndMinutes(string text, bool valid)
        {
            Assert.Equal(valid, BrightnessScheduleCalculator.ParseTime(text) != null);
        }
    }
}
