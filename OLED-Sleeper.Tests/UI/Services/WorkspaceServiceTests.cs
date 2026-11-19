using Moq;
using OLED_Sleeper.Features.MonitorBehavior.Models;
using OLED_Sleeper.Features.MonitorInformation.Models;
using OLED_Sleeper.Features.MonitorInformation.Services.Interfaces;
using OLED_Sleeper.Features.UserSettings.Models;
using OLED_Sleeper.Features.UserSettings.Services.Interfaces;
using OLED_Sleeper.Tests.UI.Helpers;
using OLED_Sleeper.UI.Services;

namespace OLED_Sleeper.Tests.UI.Services
{
    public class WorkspaceServiceTests
    {
        private readonly Mock<IMonitorInfoManager> _monitors = new();
        private readonly Mock<IMonitorSettingsFileService> _file = new();
        private readonly List<MonitorSettings> _saved = new();
        private readonly WorkspaceService _sut;

        public WorkspaceServiceTests()
        {
            IReadOnlyList<MonitorInfo> present = new[] { TestMonitors.Info(1), TestMonitors.Info(2) };
            _monitors.Setup(m => m.GetCurrentMonitorsAsync()).ReturnsAsync(present);
            _monitors.Setup(m => m.RefreshMonitorsAsync()).ReturnsAsync(present);
            _file.Setup(f => f.LoadSettings()).Returns(_saved);
            _sut = new WorkspaceService(_monitors.Object, _file.Object, new MonitorLayoutService());
        }

        [Fact]
        public async Task Saved_settings_are_applied_by_hardware_id_and_start_clean()
        {
            _saved.Add(new MonitorSettings { HardwareId = "MON-2", IsManaged = true, Behavior = MonitorBehaviorType.Dim, DimLevel = 30 });
            _saved.Add(new MonitorSettings { HardwareId = "MON-9", IsManaged = true });

            var tiles = await _sut.BuildWorkspaceAsync(400, 200);

            Assert.Equal(2, tiles.Count);
            Assert.False(tiles[0].Configuration.IsManaged);
            Assert.True(tiles[1].Configuration.IsManaged);
            Assert.Equal(30, tiles[1].Configuration.DimLevel);
            Assert.All(tiles, t => Assert.False(t.Configuration.IsDirty));
        }

        [Fact]
        public async Task Building_uses_the_known_monitors_without_rescanning()
        {
            await _sut.BuildWorkspaceAsync(400, 200);

            _monitors.Verify(m => m.RefreshMonitorsAsync(), Times.Never);
        }

        [Fact]
        public async Task Refreshing_rescans_before_building()
        {
            var order = new List<string>();
            _monitors.Setup(m => m.RefreshMonitorsAsync())
                     .Callback(() => order.Add("refresh"))
                     .ReturnsAsync(Array.Empty<MonitorInfo>());
            _monitors.Setup(m => m.GetCurrentMonitorsAsync())
                     .Callback(() => order.Add("read"))
                     .ReturnsAsync(new[] { TestMonitors.Info(1) });

            var tiles = await _sut.RefreshWorkspaceAsync(400, 200);

            Assert.Equal(new[] { "refresh", "read" }, order);
            Assert.Single(tiles);
        }
    }
}
