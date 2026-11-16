using Moq;
using OLED_Sleeper.Features.MonitorDimming.Services;
using OLED_Sleeper.Features.MonitorDimming.Services.Interfaces;
using OLED_Sleeper.Features.MonitorInformation.Models;
using OLED_Sleeper.Features.MonitorInformation.Services.Interfaces;
using OLED_Sleeper.Tests.TestDoubles;

namespace OLED_Sleeper.Tests.Features.MonitorDimming.Services
{
    public class MonitorDimmingFadeAndScheduleTests
    {
        private const string HardwareId = "MON-OLED";
        private const string DeviceName = @"\.\DISPLAY1";

        private readonly Mock<IMonitorInfoManager> _monitorManager = new();
        private readonly Mock<IOriginalBrightnessStore> _store = new();
        private readonly FakeDdcCiAccess _ddcCiAccess = new();
        private readonly FakeDdcCiSession _panel;

        public MonitorDimmingFadeAndScheduleTests()
        {
            _monitorManager.Setup(m => m.GetCurrentMonitorsAsync()).ReturnsAsync(new[]
            {
                new MonitorInfo { HardwareId = HardwareId, DeviceName = DeviceName, Capabilities = new DdcCiCapabilities(true, 100) }
            });
            _panel = _ddcCiAccess.AddPanel(DeviceName, 80);
        }

        private MonitorDimmingService CreateService(BrightnessFadeOptions? fade = null) =>
            new(_monitorManager.Object, _ddcCiAccess, _store.Object, fade);

        [Fact]
        public async Task DimMonitorAsync_WithFading_StepsDownAndEndsOnTheTarget()
        {
            var service = CreateService(new BrightnessFadeOptions(4, TimeSpan.Zero));

            await service.DimMonitorAsync(HardwareId, 20);

            Assert.Equal(new[] { 65u, 50u, 35u, 20u }, _panel.WrittenBrightnessLevels);
        }

        [Fact]
        public async Task SetScheduledBrightnessAsync_WhenNotDimmed_WritesTheScheduledLevel()
        {
            uint ignored;
            _store.Setup(s => s.TryGetOriginal(HardwareId, out ignored)).Returns(false);
            var service = CreateService();

            await service.SetScheduledBrightnessAsync(HardwareId, 40);

            Assert.Equal(new[] { 40u }, _panel.WrittenBrightnessLevels);
            _store.Verify(s => s.ReplaceOriginal(It.IsAny<string>(), It.IsAny<uint>()), Times.Never);
        }

        [Fact]
        public async Task SetScheduledBrightnessAsync_WhenDimmed_OnlyChangesTheLevelItWakesTo()
        {
            uint original = 80;
            _store.Setup(s => s.TryGetOriginal(HardwareId, out original)).Returns(true);
            var service = CreateService();

            await service.SetScheduledBrightnessAsync(HardwareId, 40);

            Assert.Empty(_panel.WrittenBrightnessLevels);
            _store.Verify(s => s.ReplaceOriginal(HardwareId, 40u), Times.Once);
        }

        [Fact]
        public async Task SetScheduledBrightnessAsync_WhenAlreadyAtTheLevel_WritesNothing()
        {
            uint ignored;
            _store.Setup(s => s.TryGetOriginal(HardwareId, out ignored)).Returns(false);
            var service = CreateService();

            await service.SetScheduledBrightnessAsync(HardwareId, 80);

            Assert.Empty(_panel.WrittenBrightnessLevels);
        }
    }
}
