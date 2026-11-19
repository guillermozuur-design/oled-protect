using OLED_Sleeper.UI.Helpers;

namespace OLED_Sleeper.Tests.UI.Helpers
{
    public class MonitorTitleFormatterTests
    {
        [Theory]
        [InlineData(3, "", false, "Scherm 3")]
        [InlineData(1, "", true, "Scherm 1 (hoofdscherm)")]
        [InlineData(1, "1|2", false, "Scherm 1|2")]
        [InlineData(2, "1|2", true, "Scherm 1|2 (hoofdscherm)")]
        public void Titles_follow_the_Windows_numbering(int number, string topology, bool primary, string expected)
        {
            var info = TestMonitors.Info(number);
            info.TopologyLabel = topology;
            info.IsPrimary = primary;

            Assert.Equal(expected, MonitorTitleFormatter.Format(info));
        }
    }
}
