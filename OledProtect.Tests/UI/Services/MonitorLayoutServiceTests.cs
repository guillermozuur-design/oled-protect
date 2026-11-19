using OledProtect.Features.MonitorInformation.Models;
using OledProtect.Tests.UI.Helpers;
using OledProtect.UI.Services;
using System.Windows;

namespace OledProtect.Tests.UI.Services
{
    public class MonitorLayoutServiceTests
    {
        private readonly MonitorLayoutService _sut = new();

        [Fact]
        public void Without_monitors_the_layout_is_empty()
        {
            Assert.Empty(_sut.CreateLayout(new List<MonitorInfo>(), 400, 200));
            Assert.Empty(_sut.CreateLayout(null, 400, 200));
        }

        [Fact]
        public void Monitors_are_scaled_to_ninety_percent_and_centred()
        {
            // Two 1920x1080 panels side by side: a 3840x1080 desktop. In a 400x200 area the width is
            // the tighter fit (400/3840 < 200/1080), so it sets the scale and the tiles are centred vertically.
            var left = TestMonitors.Info(1, new Rect(0, 0, 1920, 1080));
            var right = TestMonitors.Info(2, new Rect(1920, 0, 1920, 1080));

            var tiles = _sut.CreateLayout(new List<MonitorInfo> { left, right }, 400, 200);

            double scale = 0.9 * 400 / 3840;
            Assert.Equal(2, tiles.Count);
            Assert.Equal(1920 * scale, tiles[0].ScaledWidth, 6);
            Assert.Equal(1080 * scale, tiles[0].ScaledHeight, 6);
            Assert.Equal(20, tiles[0].ScaledLeft, 6);
            Assert.Equal(20 + 1920 * scale, tiles[1].ScaledLeft, 6);
            Assert.Equal((200 - 1080 * scale) / 2, tiles[0].ScaledTop, 6);
        }

        [Fact]
        public void Negative_desktop_coordinates_are_shifted_into_view()
        {
            // A monitor left of and above the primary one.
            var primary = TestMonitors.Info(1, new Rect(0, 0, 1000, 1000));
            var other = TestMonitors.Info(2, new Rect(-1000, -1000, 1000, 1000));

            var tiles = _sut.CreateLayout(new List<MonitorInfo> { primary, other }, 200, 200);

            // Desktop is 2000x2000 into 200x200: scale 0.09, 180px square centred with a 10px margin.
            Assert.Equal(10, tiles[1].ScaledLeft, 6);
            Assert.Equal(10, tiles[1].ScaledTop, 6);
            Assert.Equal(100, tiles[0].ScaledLeft, 6);
            Assert.Equal(100, tiles[0].ScaledTop, 6);
        }
    }
}
