using Moq;
using OLED_Sleeper.Features.MonitorBlackout.Commands;
using OLED_Sleeper.Features.MonitorBlackout.Handlers;
using OLED_Sleeper.Features.MonitorBlackout.Models;
using OLED_Sleeper.Features.MonitorBlackout.Services.Interfaces;
using OLED_Sleeper.Features.MonitorDimming.Services.Interfaces;
using OLED_Sleeper.Features.MonitorInformation.Models;
using OLED_Sleeper.Features.MonitorInformation.Services.Interfaces;
using System.Windows;

namespace OLED_Sleeper.Tests.Features.MonitorBlackout.Handlers
{
    public class ApplyBlackoutOverlayCommandHandlerTests
    {
        private const string Id = "SCREEN-7";
        private static readonly Rect Area = new(-1920, 0, 1920, 1080);

        private readonly Mock<IMonitorInfoManager> _screens = new();
        private readonly Mock<IMonitorBlackoutService> _overlays = new();
        private readonly Mock<IMonitorDimmingService> _dimmer = new();

        private Task Run(ApplyBlackoutOverlayCommand command) =>
            new ApplyBlackoutOverlayCommandHandler(_screens.Object, _overlays.Object, _dimmer.Object).HandleAsync(command);

        private void Connect(params MonitorInfo[] monitors) =>
            _screens.Setup(s => s.GetCurrentMonitorsAsync()).ReturnsAsync(monitors.ToList());

        private static MonitorInfo Screen(bool ddc) =>
            new() { HardwareId = Id, Bounds = Area, Capabilities = new DdcCiCapabilities(ddc, ddc ? 100u : 0u) };

        [Theory]
        [InlineData(OverlayScene.Black)]
        [InlineData(OverlayScene.Stars)]
        [InlineData(OverlayScene.Flights)]
        public async Task ShowsTheOverlayWithTheScreensBoundsAndScene(OverlayScene scene)
        {
            Connect(Screen(ddc: true));

            await Run(new ApplyBlackoutOverlayCommand { HardwareId = Id, Scene = scene });

            _overlays.Verify(o => o.ShowBlackoutOverlayAsync(Id, Area, scene), Times.Once);
        }

        [Fact]
        public async Task LowerBrightness_OnADdcScreen_AlsoDimsToZero()
        {
            Connect(Screen(ddc: true));

            await Run(new ApplyBlackoutOverlayCommand { HardwareId = Id, LowerBrightness = true });

            _overlays.Verify(o => o.ShowBlackoutOverlayAsync(Id, Area, OverlayScene.Black), Times.Once);
            _dimmer.Verify(d => d.DimMonitorAsync(Id, 0), Times.Once);
        }

        [Fact]
        public async Task LowerBrightness_OnAScreenWithoutDdc_OnlyShowsTheOverlay()
        {
            Connect(Screen(ddc: false));

            await Run(new ApplyBlackoutOverlayCommand { HardwareId = Id, LowerBrightness = true });

            _overlays.Verify(o => o.ShowBlackoutOverlayAsync(Id, Area, OverlayScene.Black), Times.Once);
            _dimmer.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task WithoutLowerBrightness_NeverTouchesTheBacklight()
        {
            Connect(Screen(ddc: true));

            await Run(new ApplyBlackoutOverlayCommand { HardwareId = Id });

            _dimmer.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task AnUnknownScreen_IsSkippedQuietly()
        {
            Connect();

            Exception? error = await Record.ExceptionAsync(() => Run(new ApplyBlackoutOverlayCommand { HardwareId = Id, LowerBrightness = true }));

            Assert.Null(error);
            _overlays.VerifyNoOtherCalls();
            _dimmer.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task AFailingOverlay_IsSwallowed()
        {
            Connect(Screen(ddc: false));
            _overlays.Setup(o => o.ShowBlackoutOverlayAsync(It.IsAny<string>(), It.IsAny<Rect>(), It.IsAny<OverlayScene>()))
                     .ThrowsAsync(new InvalidOperationException("no window"));

            Exception? error = await Record.ExceptionAsync(() => Run(new ApplyBlackoutOverlayCommand { HardwareId = Id }));

            Assert.Null(error);
        }

        [Fact]
        public async Task AFailingScreenScan_IsSwallowedAndNothingHappens()
        {
            _screens.Setup(s => s.GetCurrentMonitorsAsync()).ThrowsAsync(new InvalidOperationException("enumeration failed"));

            Exception? error = await Record.ExceptionAsync(() => Run(new ApplyBlackoutOverlayCommand { HardwareId = Id }));

            Assert.Null(error);
            _overlays.VerifyNoOtherCalls();
            _dimmer.VerifyNoOtherCalls();
        }
    }
}
