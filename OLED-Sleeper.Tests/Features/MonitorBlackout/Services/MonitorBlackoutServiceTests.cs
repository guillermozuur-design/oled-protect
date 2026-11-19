using OLED_Sleeper.Features.MonitorBlackout.Models;
using OLED_Sleeper.Features.MonitorBlackout.Services;
using OLED_Sleeper.Tests.TestDoubles;
using System.Windows;

namespace OLED_Sleeper.Tests.Features.MonitorBlackout.Services
{
    public class MonitorBlackoutServiceTests
    {
        private static readonly Rect Left = new(0, 0, 1920, 1080);
        private static readonly Rect Right = new(1920, 0, 2560, 1440);

        private readonly ImmediateDispatcher _ui = new();
        private readonly FakeOverlayWindowFactory _windows = new();
        private readonly MonitorBlackoutService _sut;

        public MonitorBlackoutServiceTests()
        {
            _sut = new MonitorBlackoutService(_ui, _windows);
        }

        [Fact]
        public async Task Show_PutsOneOverlayOverTheRequestedArea()
        {
            await _sut.ShowBlackoutOverlayAsync("A", Left);

            FakeOverlayWindow window = Assert.Single(_windows.Created);
            Assert.Equal(Left, window.ShownBounds);
            Assert.False(window.IsClosed);
            Assert.True(_sut.IsOverlayWindow(window.Handle));
        }

        [Fact]
        public async Task Show_PassesTheSceneToTheFactory()
        {
            await _sut.ShowBlackoutOverlayAsync("A", Left, OverlayScene.Flights);

            Assert.Equal([OverlayScene.Flights], _windows.RequestedScenes);
        }

        [Fact]
        public async Task ShowAndHide_AlwaysGoThroughTheDispatcher()
        {
            _ui.IsOnUiThread = false;

            await _sut.ShowBlackoutOverlayAsync("A", Left);
            Assert.Equal(1, _ui.InvokeAsyncCount);

            await _sut.HideBlackoutOverlayAsync("A");
            Assert.Equal(2, _ui.InvokeAsyncCount);
        }

        [Fact]
        public async Task Show_ForACoveredScreen_KeepsTheExistingOverlay()
        {
            await _sut.ShowBlackoutOverlayAsync("A", Left);
            await _sut.ShowBlackoutOverlayAsync("A", Right);

            FakeOverlayWindow window = Assert.Single(_windows.Created);
            Assert.Equal(Left, window.ShownBounds);
        }

        [Fact]
        public async Task Show_ForTwoScreens_TracksBothHandles()
        {
            await _sut.ShowBlackoutOverlayAsync("A", Left);
            await _sut.ShowBlackoutOverlayAsync("B", Right);

            Assert.Equal(2, _windows.Created.Count);
            Assert.All(_windows.Created, w => Assert.True(_sut.IsOverlayWindow(w.Handle)));
            Assert.NotEqual(_windows.Created[0].Handle, _windows.Created[1].Handle);
        }

        [Fact]
        public async Task Show_WithAWindowThatNeverGotAHandle_StillCountsAsCovered()
        {
            var handleless = new FakeOverlayWindowFactory(nint.Zero);
            var sut = new MonitorBlackoutService(_ui, handleless);

            await sut.ShowBlackoutOverlayAsync("A", Left);
            await sut.ShowBlackoutOverlayAsync("A", Left);

            Assert.Single(handleless.Created);
            Assert.False(sut.IsOverlayWindow(nint.Zero));
        }

        [Fact]
        public async Task Hide_ClosesTheOverlayAndForgetsTheHandleItHadWhileShown()
        {
            await _sut.ShowBlackoutOverlayAsync("A", Left);
            FakeOverlayWindow window = _windows.Created[0];
            nint handleWhileShown = window.Handle;

            await _sut.HideBlackoutOverlayAsync("A");

            Assert.True(window.IsClosed);
            Assert.Equal(nint.Zero, window.Handle);
            Assert.False(_sut.IsOverlayWindow(handleWhileShown));
        }

        [Fact]
        public async Task Hide_OnlyTouchesTheRequestedScreen()
        {
            await _sut.ShowBlackoutOverlayAsync("A", Left);
            await _sut.ShowBlackoutOverlayAsync("B", Right);
            FakeOverlayWindow other = _windows.Created[1];

            await _sut.HideBlackoutOverlayAsync("A");

            Assert.False(other.IsClosed);
            Assert.True(_sut.IsOverlayWindow(other.Handle));
        }

        [Fact]
        public async Task Hide_ForAnUncoveredScreen_IsANoOp()
        {
            Exception? error = await Record.ExceptionAsync(() => _sut.HideBlackoutOverlayAsync("nobody"));

            Assert.Null(error);
            Assert.Empty(_windows.Created);
        }

        [Fact]
        public async Task Hide_Twice_ClosesOnlyOnce()
        {
            await _sut.ShowBlackoutOverlayAsync("A", Left);

            await _sut.HideBlackoutOverlayAsync("A");
            Exception? error = await Record.ExceptionAsync(() => _sut.HideBlackoutOverlayAsync("A"));

            Assert.Null(error);
            Assert.Equal(1, Assert.Single(_windows.Created).CloseCalls);
        }

        [Fact]
        public async Task Show_AfterHide_BuildsANewOverlay()
        {
            await _sut.ShowBlackoutOverlayAsync("A", Left);
            await _sut.HideBlackoutOverlayAsync("A");

            await _sut.ShowBlackoutOverlayAsync("A", Right);

            Assert.Equal(2, _windows.Created.Count);
            FakeOverlayWindow fresh = _windows.Created[1];
            Assert.Equal(Right, fresh.ShownBounds);
            Assert.True(_sut.IsOverlayWindow(fresh.Handle));
        }

        [Fact]
        public async Task IsOverlayWindow_RejectsZeroAndForeignHandles()
        {
            Assert.False(_sut.IsOverlayWindow(nint.Zero));

            await _sut.ShowBlackoutOverlayAsync("A", Left);
            nint ours = _windows.Created[0].Handle;

            Assert.False(_sut.IsOverlayWindow(nint.Zero));
            Assert.False(_sut.IsOverlayWindow(ours + 1));
        }
    }
}
