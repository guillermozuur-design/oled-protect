using Moq;
using OLED_Sleeper.Features.MonitorDimming.Services;
using OLED_Sleeper.Features.MonitorDimming.Services.Interfaces;
using OLED_Sleeper.Features.MonitorInformation.Models;
using OLED_Sleeper.Features.MonitorInformation.Services.Interfaces;
using OLED_Sleeper.Tests.TestDoubles;

namespace OLED_Sleeper.Tests.Features.MonitorDimming.Services
{
    // Two attached 0-255 panels, both at 80, behind fake DDC/CI channels; the original-brightness store is a mock.
    public sealed class MonitorDimmingServiceTests
    {
        private const string Left = "PNP-LEFT";
        private const string LeftDevice = @"\\.\DISPLAY1";
        private const string Right = "PNP-RIGHT";
        private const string RightDevice = @"\\.\DISPLAY2";
        private const uint PanelRange = 255;

        private readonly Mock<IMonitorInfoManager> _attached = new();
        private readonly Mock<IOriginalBrightnessStore> _originals = new();
        private readonly FakeDdcCiAccess _bus = new();
        private readonly FakeDdcCiSession _leftPanel;
        private readonly FakeDdcCiSession _rightPanel;
        private readonly MonitorDimmingService _sut;

        public MonitorDimmingServiceTests()
        {
            Attach(Monitor(Left, LeftDevice, PanelRange), Monitor(Right, RightDevice, PanelRange));
            _leftPanel = _bus.AddPanel(LeftDevice, 80);
            _rightPanel = _bus.AddPanel(RightDevice, 80);
            _sut = new MonitorDimmingService(_attached.Object, _bus, _originals.Object);
        }

        // ----- Dimming -----

        [Fact]
        public async Task Dim_RemembersTheLevelBeforeTheFirstWrite_ThenWritesThePercentOfTheRange()
        {
            int writesWhenRecorded = -1;
            _originals.Setup(o => o.RecordOriginal(Left, 80u))
                .Callback(() => writesWhenRecorded = _leftPanel.WrittenBrightnessLevels.Count);

            await _sut.DimMonitorAsync(Left, 15);

            _originals.Verify(o => o.RecordOriginal(Left, 80u), Times.Once);
            Assert.Equal(0, writesWhenRecorded);
            Assert.Equal(new[] { 38u }, _leftPanel.WrittenBrightnessLevels); // 15% of 255, rounded
        }

        [Fact]
        public async Task Dim_PanelDoesNotAnswerTheRead_NeitherRecordsNorWrites()
        {
            _leftPanel.IgnoresReads = true;

            await _sut.DimMonitorAsync(Left, 15);

            _originals.Verify(o => o.RecordOriginal(It.IsAny<string>(), It.IsAny<uint>()), Times.Never);
            Assert.Empty(_leftPanel.WrittenBrightnessLevels);
        }

        [Fact]
        public async Task Dim_PanelWithoutAReportedRange_GetsThePercentAsIs()
        {
            Attach(Monitor(Left, LeftDevice, 0));

            await _sut.DimMonitorAsync(Left, 15);

            Assert.Equal(new[] { 15u }, _leftPanel.WrittenBrightnessLevels);
        }

        [Fact]
        public async Task Dim_RefusedWrite_KeepsTheRecordingAndDoesNotTryAgain()
        {
            _leftPanel.RefusesWrites = true;

            await _sut.DimMonitorAsync(Left, 15);

            _originals.Verify(o => o.RecordOriginal(Left, 80u), Times.Once);
            Assert.Equal(new[] { 38u }, _leftPanel.WrittenBrightnessLevels);
            Assert.Single(_bus.OpenRequests);
        }

        [Theory]
        [InlineData((string?)null)]
        [InlineData("")]
        public async Task Dim_WithoutHardwareId_LeavesTheBusAlone(string? hardwareId)
        {
            await _sut.DimMonitorAsync(hardwareId, 15);

            Assert.Empty(_bus.OpenRequests);
        }

        [Fact]
        public async Task Dim_MonitorThatIsNotAttached_LeavesTheBusAlone()
        {
            await _sut.DimMonitorAsync("PNP-GONE", 15);

            Assert.Empty(_bus.OpenRequests);
        }

        // ----- Undimming -----

        [Fact]
        public async Task Undim_ConfirmedRestore_WritesTheRecordedRawLevelAndForgetsIt()
        {
            Remember(Left, 80);

            await _sut.UndimMonitorAsync(Left);

            Assert.Equal(new[] { 80u }, _leftPanel.WrittenBrightnessLevels);
            _originals.Verify(o => o.RemoveOriginal(Left), Times.Once);
        }

        [Fact]
        public async Task Undim_ReadBackTwoOff_StillCountsAsRestored()
        {
            Remember(Left, 80);
            _leftPanel.SettlesAt = 82;

            await _sut.UndimMonitorAsync(Left);

            _originals.Verify(o => o.RemoveOriginal(Left), Times.Once);
        }

        [Fact]
        public async Task Undim_ReadBackThreeOff_TriesThreeTimesAndKeepsTheRecording()
        {
            Remember(Left, 80);
            _leftPanel.SettlesAt = 83;

            await _sut.UndimMonitorAsync(Left);

            Assert.Equal(3, _leftPanel.WrittenBrightnessLevels.Count);
            AssertRecordingKept();
        }

        [Fact]
        public async Task Undim_ReadBackUnanswered_TriesThreeTimesAndKeepsTheRecording()
        {
            Remember(Left, 80);
            _leftPanel.IgnoresReads = true;

            await _sut.UndimMonitorAsync(Left);

            Assert.Equal(new[] { 80u, 80u, 80u }, _leftPanel.WrittenBrightnessLevels);
            AssertRecordingKept();
        }

        [Fact]
        public async Task Undim_RefusedWrites_TriesThreeTimesAndKeepsTheRecording()
        {
            Remember(Left, 80);
            _leftPanel.RefusesWrites = true;

            await _sut.UndimMonitorAsync(Left);

            Assert.Equal(3, _leftPanel.WrittenBrightnessLevels.Count);
            AssertRecordingKept();
        }

        [Fact]
        public async Task Undim_EveryTryFails_DisposesEachChannelItOpened()
        {
            Remember(Left, 80);
            _leftPanel.RefusesWrites = true;

            await _sut.UndimMonitorAsync(Left);

            Assert.Equal(3, _bus.OpenRequests.Count);
            Assert.Equal(3, _leftPanel.Disposals);
        }

        [Fact]
        public async Task Undim_UnreachablePanel_GivesUpAfterOneTryAndKeepsTheRecording()
        {
            const string offline = "PNP-OFFLINE";
            Attach(Monitor(offline, @"\\.\DISPLAY7", PanelRange));
            Remember(offline, 80);

            await _sut.UndimMonitorAsync(offline);

            Assert.Single(_bus.OpenRequests);
            AssertRecordingKept();
        }

        [Fact]
        public async Task Undim_MonitorThatWasNeverDimmed_LeavesTheBusAlone()
        {
            await _sut.UndimMonitorAsync(Left);

            Assert.Empty(_bus.OpenRequests);
        }

        [Theory]
        [InlineData((string?)null)]
        [InlineData("")]
        public async Task Undim_WithoutHardwareId_LeavesTheBusAlone(string? hardwareId)
        {
            await _sut.UndimMonitorAsync(hardwareId!);

            Assert.Empty(_bus.OpenRequests);
        }

        // ----- Schedule and bookkeeping -----

        [Theory]
        [InlineData((string?)null)]
        [InlineData("")]
        public async Task Schedule_WithoutHardwareId_ReportsFailureWithoutTouchingTheBus(string? hardwareId)
        {
            bool applied = await _sut.SetScheduledBrightnessAsync(hardwareId!, 40);

            Assert.False(applied);
            Assert.Empty(_bus.OpenRequests);
        }

        [Fact]
        public async Task Schedule_UnreachablePanel_ReportsFailure()
        {
            Attach(Monitor("PNP-OFFLINE", @"\\.\DISPLAY7", PanelRange));

            Assert.False(await _sut.SetScheduledBrightnessAsync("PNP-OFFLINE", 40));
        }

        [Fact]
        public void GetDimmedMonitors_PassesOnWhatTheStoreHolds()
        {
            var held = new Dictionary<string, uint> { [Left] = 80 };
            _originals.Setup(o => o.GetAll()).Returns(held);

            Assert.Equal(held, _sut.GetDimmedMonitors());
        }

        // ----- One operation per monitor at a time -----

        [Fact]
        public async Task SecondDimOfTheSameMonitor_WaitsUntilTheFirstIsDone()
        {
            using var unpark = new ManualResetEventSlim();
            var firstInside = BlockInsideLeftChannel(unpark);
            var first = Task.Run(() => _sut.DimMonitorAsync(Left, 15));
            await firstInside;

            var second = Task.Run(() => _sut.DimMonitorAsync(Left, 15));

            Assert.True(await StillWaiting(second));
            Assert.Single(_bus.OpenRequests);

            unpark.Set();
            await Task.WhenAll(first, second);
        }

        [Fact]
        public async Task UndimOfTheSameMonitor_WaitsForTheRunningDim()
        {
            Remember(Left, 80);
            using var unpark = new ManualResetEventSlim();
            var dimInside = BlockInsideLeftChannel(unpark);
            var dim = Task.Run(() => _sut.DimMonitorAsync(Left, 15));
            await dimInside;

            var undim = Task.Run(() => _sut.UndimMonitorAsync(Left));

            Assert.True(await StillWaiting(undim));
            Assert.Single(_bus.OpenRequests);

            unpark.Set();
            await Task.WhenAll(dim, undim);
            Assert.Equal(new[] { 38u, 80u }, _leftPanel.WrittenBrightnessLevels);
        }

        [Fact]
        public async Task DimOfAnotherMonitor_DoesNotWaitForTheBusyOne()
        {
            using var unpark = new ManualResetEventSlim();
            var leftInside = BlockInsideLeftChannel(unpark);
            var left = Task.Run(() => _sut.DimMonitorAsync(Left, 15));
            await leftInside;

            await _sut.DimMonitorAsync(Right, 15);

            Assert.Equal(new[] { 38u }, _rightPanel.WrittenBrightnessLevels);
            unpark.Set();
            await left;
        }

        // ----- Helpers -----

        private static MonitorInfo Monitor(string id, string device, uint range) => new()
        {
            HardwareId = id,
            DeviceName = device,
            Capabilities = new DdcCiCapabilities(true, range),
        };

        private void Attach(params MonitorInfo[] monitors) =>
            _attached.Setup(m => m.GetCurrentMonitorsAsync()).ReturnsAsync(monitors);

        private void Remember(string hardwareId, uint level) =>
            _originals.Setup(o => o.TryGetOriginal(hardwareId, out level)).Returns(true);

        // True when the task has not finished after a short grace period, i.e. it is queued behind something.
        private static async Task<bool> StillWaiting(Task operation) =>
            await Task.WhenAny(operation, Task.Delay(TimeSpan.FromMilliseconds(100))) != operation;

        private void AssertRecordingKept() =>
            _originals.Verify(o => o.RemoveOriginal(It.IsAny<string>()), Times.Never);

        // Parks the first open of the left panel until unpark is set, holding that monitor's lock meanwhile.
        // The returned task completes once the operation is parked.
        private Task BlockInsideLeftChannel(ManualResetEventSlim unpark)
        {
            var parked = new TaskCompletionSource();
            _bus.WhileOpening = device =>
            {
                if (device != LeftDevice) return;

                parked.TrySetResult();
                unpark.Wait();
            };

            return parked.Task;
        }
    }
}
