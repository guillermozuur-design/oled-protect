using Moq;
using OLED_Sleeper.Features.UserSettings.Models;
using OLED_Sleeper.Features.UserSettings.Services.Interfaces;
using OLED_Sleeper.Tests.UI.Helpers;
using OLED_Sleeper.UI.Services;
using OLED_Sleeper.UI.Services.Interfaces;
using OLED_Sleeper.UI.ViewModels;

namespace OLED_Sleeper.Tests.UI.Services
{
    public class MonitorSettingsSaveServiceTests
    {
        private readonly Mock<IMonitorSettingsFileService> _file = new();
        private readonly Mock<IDialogService> _dialogs = new();
        private readonly List<List<MonitorSettings>> _writes = new();
        private readonly MonitorSettingsSaveService _sut;

        public MonitorSettingsSaveServiceTests()
        {
            _file.Setup(f => f.SaveSettings(It.IsAny<List<MonitorSettings>>()))
                 .Callback<List<MonitorSettings>>(_writes.Add);
            _sut = new MonitorSettingsSaveService(_file.Object, _dialogs.Object);
        }

        [Fact]
        public void A_valid_configuration_is_written_once_with_its_values()
        {
            var tile = TestMonitors.ValidManagedTile(1);
            tile.Configuration.DimLevel = 50;

            Assert.True(_sut.TrySave(new[] { tile }));

            var written = Assert.Single(Assert.Single(_writes));
            Assert.Equal("MON-1", written.HardwareId);
            Assert.Equal(50, written.DimLevel);
        }

        [Fact]
        public void Every_monitor_is_clean_after_a_save()
        {
            var edited = TestMonitors.ValidManagedTile(1);
            var untouched = TestMonitors.ValidManagedTile(2);
            edited.Configuration.DimLevel = 50;
            Assert.True(edited.Configuration.IsDirty);

            _sut.TrySave(new[] { edited, untouched });

            Assert.False(edited.Configuration.IsDirty);
            Assert.False(untouched.Configuration.IsDirty);
        }

        [Fact]
        public void An_invalid_managed_monitor_blocks_the_write_and_is_reported()
        {
            var tile = TestMonitors.ValidManagedTile(1);
            tile.Configuration.IdleValue = 0;

            Assert.False(_sut.TrySave(new[] { tile }));

            Assert.Empty(_writes);
            _dialogs.Verify(d => d.ShowError(It.Is<string>(m => m.Contains("Scherm 1")), "Instellingen niet compleet"), Times.Once);
            Assert.True(tile.Configuration.IsDirty);
        }

        [Fact]
        public void An_invalid_unmanaged_monitor_does_not_block_the_write()
        {
            var tile = TestMonitors.Tile(1);
            tile.Configuration.IdleValue = 0;

            Assert.True(_sut.TrySave(new[] { tile }));

            Assert.Single(_writes);
            _dialogs.Verify(d => d.ShowError(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void Saving_no_monitors_writes_an_empty_list()
        {
            Assert.True(_sut.TrySave(Array.Empty<MonitorLayoutViewModel>()));

            Assert.Empty(Assert.Single(_writes));
        }
    }
}
