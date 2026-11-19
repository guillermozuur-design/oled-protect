using OledProtect.Features.MonitorBlackout.Models;
using OledProtect.Features.MonitorBlackout.Services;
using OledProtect.Tests.TestDoubles;
using System.Windows;
using Phase = OledProtect.Tests.TestDoubles.FakeOverlayWindow.Phase;

namespace OledProtect.Tests.Features.MonitorBlackout.Services
{
    public class MonitorBlackoutServiceTests
    {
        private static readonly Rect Laptop = new(0, 0, 1920, 1200);
        private static readonly Rect Ultrawide = new(1920, -240, 3440, 1440);

        private readonly ImmediateDispatcher _uiQueue = new();
        private readonly FakeOverlayWindowFactory _factory;
        private readonly MonitorBlackoutService _covers;

        public MonitorBlackoutServiceTests() : this(withHandles: true)
        {
        }

        private MonitorBlackoutServiceTests(bool withHandles)
        {
            _factory = new FakeOverlayWindowFactory(withHandles);
            _covers = new MonitorBlackoutService(_uiQueue, _factory);
        }

        private FakeOverlayWindow OnlyWindow => Assert.Single(_factory.Built);

        [Fact]
        public async Task Covering_a_screen_shows_one_window_on_its_area()
        {
            await _covers.ShowBlackoutOverlayAsync("left", Laptop);

            Assert.Equal(Phase.Visible, OnlyWindow.State);
            Assert.Equal(Laptop, OnlyWindow.Area);
            Assert.True(_covers.IsOverlayWindow(OnlyWindow.AssignedHandle));
        }

        [Theory]
        [InlineData(OverlayScene.Black)]
        [InlineData(OverlayScene.Stars)]
        [InlineData(OverlayScene.Flights)]
        public async Task The_requested_scene_reaches_the_window(OverlayScene scene)
        {
            await _covers.ShowBlackoutOverlayAsync("left", Laptop, scene);

            Assert.Equal(scene, OnlyWindow.Scene);
        }

        [Fact]
        public async Task Without_a_scene_the_cover_is_plain_black()
        {
            await _covers.ShowBlackoutOverlayAsync("left", Laptop);

            Assert.Equal(OverlayScene.Black, OnlyWindow.Scene);
        }

        [Fact]
        public async Task Window_work_is_queued_on_the_uiQueue_even_from_a_worker_thread()
        {
            _uiQueue.IsOnUiThread = false;

            await _covers.ShowBlackoutOverlayAsync("left", Laptop);
            await _covers.HideBlackoutOverlayAsync("left");

            Assert.Equal([ImmediateDispatcher.Entry.InvokeAsync, ImmediateDispatcher.Entry.InvokeAsync], _uiQueue.Journal);
        }

        [Fact]
        public async Task Covering_an_already_covered_screen_leaves_the_first_window_alone()
        {
            await _covers.ShowBlackoutOverlayAsync("left", Laptop, OverlayScene.Stars);
            await _covers.ShowBlackoutOverlayAsync("left", Ultrawide, OverlayScene.Flights);

            Assert.Equal(Laptop, OnlyWindow.Area);
            Assert.Equal(OverlayScene.Stars, OnlyWindow.Scene);
        }

        [Fact]
        public async Task Each_screen_gets_its_own_window_and_handle()
        {
            await _covers.ShowBlackoutOverlayAsync("left", Laptop);
            await _covers.ShowBlackoutOverlayAsync("right", Ultrawide);

            nint[] handles = _factory.Built.Select(w => w.AssignedHandle).ToArray();
            Assert.Equal(2, handles.Distinct().Count());
            Assert.All(handles, h => Assert.True(_covers.IsOverlayWindow(h)));
        }

        [Fact]
        public async Task A_window_without_a_handle_still_marks_the_screen_as_covered()
        {
            var handleless = new MonitorBlackoutServiceTests(withHandles: false);

            await handleless._covers.ShowBlackoutOverlayAsync("left", Laptop);
            await handleless._covers.ShowBlackoutOverlayAsync("left", Laptop);

            Assert.Single(handleless._factory.Built);
            Assert.False(handleless._covers.IsOverlayWindow(0));
        }

        [Fact]
        public async Task Uncovering_closes_the_window_and_its_old_handle_is_no_longer_ours()
        {
            await _covers.ShowBlackoutOverlayAsync("left", Laptop);
            nint formerHandle = OnlyWindow.Handle;

            await _covers.HideBlackoutOverlayAsync("left");

            Assert.Equal(Phase.Closed, OnlyWindow.State);
            Assert.Equal(0, OnlyWindow.Handle);
            Assert.False(_covers.IsOverlayWindow(formerHandle));
        }

        [Fact]
        public async Task Uncovering_one_screen_keeps_the_other_covered()
        {
            await _covers.ShowBlackoutOverlayAsync("left", Laptop);
            await _covers.ShowBlackoutOverlayAsync("right", Ultrawide);

            await _covers.HideBlackoutOverlayAsync("right");

            FakeOverlayWindow left = _factory.Built[0];
            Assert.Equal(Phase.Visible, left.State);
            Assert.True(_covers.IsOverlayWindow(left.Handle));
            Assert.Equal(Phase.Closed, _factory.Built[1].State);
        }

        [Fact]
        public async Task Uncovering_a_screen_that_was_never_covered_does_nothing()
        {
            await _covers.HideBlackoutOverlayAsync("ghost");

            Assert.Empty(_factory.Built);
        }

        [Fact]
        public async Task Uncovering_twice_closes_the_window_once()
        {
            await _covers.ShowBlackoutOverlayAsync("left", Laptop);

            await _covers.HideBlackoutOverlayAsync("left");
            await _covers.HideBlackoutOverlayAsync("left");

            Assert.Equal(1, OnlyWindow.Closes);
        }

        [Fact]
        public async Task Covering_again_after_uncovering_builds_a_fresh_window()
        {
            await _covers.ShowBlackoutOverlayAsync("left", Laptop);
            await _covers.HideBlackoutOverlayAsync("left");
            await _covers.ShowBlackoutOverlayAsync("left", Ultrawide);

            Assert.Collection(
                _factory.Built,
                old => Assert.Equal(Phase.Closed, old.State),
                fresh =>
                {
                    Assert.Equal(Ultrawide, fresh.Area);
                    Assert.True(_covers.IsOverlayWindow(fresh.Handle));
                });
        }

        [Fact]
        public async Task Zero_and_unknown_handles_are_never_ours()
        {
            Assert.False(_covers.IsOverlayWindow(0));

            await _covers.ShowBlackoutOverlayAsync("left", Laptop);

            Assert.False(_covers.IsOverlayWindow(0));
            Assert.False(_covers.IsOverlayWindow(OnlyWindow.AssignedHandle + 7));
        }
    }
}
