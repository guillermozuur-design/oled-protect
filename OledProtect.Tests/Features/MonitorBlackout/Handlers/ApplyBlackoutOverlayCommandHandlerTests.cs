using Moq;
using OledProtect.Features.MonitorBlackout.Commands;
using OledProtect.Features.MonitorBlackout.Handlers;
using OledProtect.Features.MonitorBlackout.Models;
using OledProtect.Features.MonitorDimming.Services.Interfaces;
using OledProtect.Features.MonitorInformation.Models;
using OledProtect.Features.MonitorInformation.Services.Interfaces;
using OledProtect.Tests.TestDoubles;
using System.Windows;
using Request = OledProtect.Tests.TestDoubles.RecordingBlackoutService.Request;

namespace OledProtect.Tests.Features.MonitorBlackout.Handlers
{
    public class ApplyBlackoutOverlayCommandHandlerTests
    {
        private const string Oled = "LG-OLED-42";
        private static readonly Rect OledArea = new(2560, 0, 3840, 2160);

        private readonly RecordingBlackoutService _blackout = new();
        private readonly List<(string? Id, int Percent)> _dims = [];
        private readonly Mock<IMonitorDimmingService> _dimmer = new(MockBehavior.Strict);
        private Func<Task<IReadOnlyList<MonitorInfo>>> _scan = () => Task.FromResult<IReadOnlyList<MonitorInfo>>([]);

        public ApplyBlackoutOverlayCommandHandlerTests()
        {
            // Strict: any dimmer call other than this one fails the test.
            _dimmer.Setup(d => d.DimMonitorAsync(It.IsAny<string?>(), It.IsAny<int>()))
                   .Callback((string? id, int percent) => _dims.Add((id, percent)))
                   .Returns(Task.CompletedTask);
        }

        private void Attached(bool speaksDdc) =>
            _scan = () => Task.FromResult<IReadOnlyList<MonitorInfo>>(
            [
                new MonitorInfo { HardwareId = "OTHER", Bounds = new Rect(0, 0, 2560, 1440) },
                new MonitorInfo { HardwareId = Oled, Bounds = OledArea, Capabilities = new DdcCiCapabilities(speaksDdc, speaksDdc ? 100u : 0u) },
            ]);

        private Task Handle(ApplyBlackoutOverlayCommand command)
        {
            var screens = new Mock<IMonitorInfoManager>();
            screens.Setup(s => s.GetCurrentMonitorsAsync()).Returns(() => _scan());
            return new ApplyBlackoutOverlayCommandHandler(screens.Object, _blackout, _dimmer.Object).HandleAsync(command);
        }

        [Theory]
        [InlineData(OverlayScene.Black)]
        [InlineData(OverlayScene.Stars)]
        [InlineData(OverlayScene.Flights)]
        public async Task Covers_the_screen_at_its_own_area_with_the_chosen_scene(OverlayScene scene)
        {
            Attached(speaksDdc: true);

            await Handle(new ApplyBlackoutOverlayCommand { HardwareId = Oled, Scene = scene });

            Assert.Equal([new Request("show", Oled, OledArea, scene)], _blackout.Requests);
        }

        [Fact]
        public async Task Lowering_brightness_on_a_ddc_panel_also_dims_it_to_zero()
        {
            Attached(speaksDdc: true);

            await Handle(new ApplyBlackoutOverlayCommand { HardwareId = Oled, LowerBrightness = true });

            Assert.Equal([new Request("show", Oled, OledArea, OverlayScene.Black)], _blackout.Requests);
            Assert.Equal([(Oled, 0)], _dims);
        }

        [Fact]
        public async Task Lowering_brightness_without_ddc_only_covers_the_screen()
        {
            Attached(speaksDdc: false);

            await Handle(new ApplyBlackoutOverlayCommand { HardwareId = Oled, LowerBrightness = true });

            Assert.Single(_blackout.Requests);
            Assert.Empty(_dims);
        }

        [Fact]
        public async Task The_backlight_stays_untouched_unless_asked()
        {
            Attached(speaksDdc: true);

            await Handle(new ApplyBlackoutOverlayCommand { HardwareId = Oled });

            Assert.Empty(_dims);
        }

        [Fact]
        public async Task A_screen_that_is_not_attached_is_left_alone()
        {
            await Handle(new ApplyBlackoutOverlayCommand { HardwareId = Oled, LowerBrightness = true });

            Assert.Empty(_blackout.Requests);
            Assert.Empty(_dims);
        }

        [Fact]
        public async Task An_overlay_failure_does_not_reach_the_caller()
        {
            Attached(speaksDdc: false);
            _blackout.FailWith = new InvalidOperationException("dispatcher shut down");

            await Handle(new ApplyBlackoutOverlayCommand { HardwareId = Oled });

            Assert.Single(_blackout.Requests);
        }

        [Fact]
        public async Task A_failing_screen_scan_is_absorbed_and_nothing_is_done()
        {
            _scan = () => throw new InvalidOperationException("display enumeration failed");

            await Handle(new ApplyBlackoutOverlayCommand { HardwareId = Oled, LowerBrightness = true });

            Assert.Empty(_blackout.Requests);
            Assert.Empty(_dims);
        }
    }
}
