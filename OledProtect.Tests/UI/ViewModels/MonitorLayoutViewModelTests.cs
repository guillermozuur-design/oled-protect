using OledProtect.Features.MonitorInformation.Models;
using OledProtect.UI.ViewModels;
using System.Windows;

namespace OledProtect.Tests.UI.ViewModels
{
    public class MonitorLayoutViewModelTests
    {
        [Fact]
        public void Tile_is_scaled_and_shifted_into_the_panel()
        {
            var desktop = new Rect(-1920, 0, 3840, 1080);
            var info = Screen(new Rect(0, 0, 1920, 1080));

            var tile = new MonitorLayoutViewModel(info, 0.25, desktop, 10, 20);

            Assert.Equal(10 + 0.25 * 1920, tile.ScaledLeft);
            Assert.Equal(20, tile.ScaledTop);
            Assert.Equal(480, tile.ScaledWidth);
            Assert.Equal(270, tile.ScaledHeight);
        }

        [Fact]
        public void Tile_exposes_the_screen_identity()
        {
            var info = Screen(new Rect(0, 0, 2560, 1440), id: "DEL-42", number: 2, primary: true);

            var tile = new MonitorLayoutViewModel(info, 1, info.Bounds, 0, 0);

            Assert.Equal("DEL-42", tile.HardwareId);
            Assert.Equal(2, tile.DisplayNumber);
            Assert.Equal("Scherm 2 (hoofdscherm)", tile.MonitorTitle);
            Assert.Equal("2560x1440", tile.ResolutionText);
            Assert.NotNull(tile.BackgroundColor);
            Assert.True(tile.BackgroundColor.IsFrozen);
        }

        [Fact]
        public void Tile_owns_a_configuration_for_the_same_screen()
        {
            var info = Screen(new Rect(0, 0, 1920, 1080), id: "X");

            var tile = new MonitorLayoutViewModel(info, 1, info.Bounds, 0, 0);

            Assert.Equal("X", tile.Configuration.ToSettings().HardwareId);
            Assert.False(tile.IsDirty);
        }

        [Fact]
        public void Configuration_edits_surface_as_tile_dirty_state()
        {
            var tile = new MonitorLayoutViewModel(Screen(new Rect(0, 0, 1920, 1080)), 1, new Rect(0, 0, 1920, 1080), 0, 0);
            var seen = new List<string?>();
            tile.PropertyChanged += (_, e) => seen.Add(e.PropertyName);

            tile.Configuration.IdleValue = 77;

            Assert.True(tile.IsDirty);
            Assert.Contains(nameof(MonitorLayoutViewModel.IsDirty), seen);
        }

        [Fact]
        public void Selection_is_announced_only_when_it_changes()
        {
            var tile = new MonitorLayoutViewModel(Screen(new Rect(0, 0, 1920, 1080)), 1, new Rect(0, 0, 1920, 1080), 0, 0);
            int raised = 0;
            tile.PropertyChanged += (_, e) => raised += e.PropertyName == nameof(MonitorLayoutViewModel.IsSelected) ? 1 : 0;

            tile.IsSelected = true;
            tile.IsSelected = true;

            Assert.True(tile.IsSelected);
            Assert.Equal(1, raised);
        }

        private static MonitorInfo Screen(Rect bounds, string id = "HW", int number = 1, bool primary = false) =>
            new() { HardwareId = id, DisplayNumber = number, IsPrimary = primary, Bounds = bounds };
    }
}
