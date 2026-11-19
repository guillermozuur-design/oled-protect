using Moq;
using OLED_Sleeper.Features.MonitorInformation.Models;
using OLED_Sleeper.Tests.TestDoubles;
using OLED_Sleeper.UI.Services.Interfaces;
using OLED_Sleeper.UI.ViewModels;
using System.Collections.ObjectModel;
using System.Windows;

namespace OLED_Sleeper.Tests.UI.ViewModels
{
    public class MainViewModelTests
    {
        private readonly Mock<IWorkspaceService> _workspace = new();
        private readonly Mock<IMonitorSettingsSaveService> _saver = new();
        private readonly ImmediateDispatcher _ui = new();
        private readonly MainViewModel _sut;
        private bool _saveAccepted = true;

        public MainViewModelTests()
        {
            Returns();
            _saver.Setup(s => s.TrySave(It.IsAny<IReadOnlyList<MonitorLayoutViewModel>>()))
                  .Returns((IReadOnlyList<MonitorLayoutViewModel> tiles) =>
                  {
                      if (!_saveAccepted) return false;
                      // The real save service resets the baseline of every screen it wrote.
                      foreach (var tile in tiles) tile.Configuration.MarkAsSaved();
                      return true;
                  });

            _sut = new MainViewModel(_workspace.Object, _saver.Object, _ui);
        }

        // ---- building the overview ----

        [Fact]
        public void Layout_shows_the_tiles_the_workspace_built()
        {
            var a = Tile("A");
            var b = Tile("B", 2);

            Show(a, b);

            Assert.Equal(new[] { a, b }, _sut.Monitors);
        }

        [Fact]
        public void A_new_layout_replaces_the_old_tiles()
        {
            Show(Tile("A"), Tile("B", 2));
            var c = Tile("C", 3);

            Show(c);

            Assert.Equal(new[] { c }, _sut.Monitors);
        }

        [Fact]
        public void Tiles_are_marshalled_to_the_ui_thread_exactly_once_when_needed()
        {
            _ui.IsOnUiThread = false;
            var a = Tile("A");

            Show(a);

            Assert.Equal(new[] { a }, _sut.Monitors);
            Assert.Equal(1, _ui.InvokeCount);
        }

        [Fact]
        public void The_dispatcher_is_skipped_on_the_ui_thread()
        {
            Show(Tile("A"));

            Assert.Single(_sut.Monitors);
            Assert.Equal(0, _ui.InvokeCount);
        }

        [Fact]
        public async Task Loading_flag_is_up_only_while_a_build_runs()
        {
            var pending = Hold();

            _sut.RecalculateLayout(640, 480);
            bool during = _sut.IsLoading;
            pending.SetResult(Tiles(Tile("A")));
            await pending.Task;

            Assert.True(during);
            Assert.False(_sut.IsLoading);
        }

        [Fact]
        public async Task A_build_that_finishes_after_a_newer_one_is_thrown_away()
        {
            var slow = Hold();
            _sut.RecalculateLayout(640, 480);
            var fresh = Tile("FRESH");
            Returns(fresh);
            _sut.RecalculateLayout(1280, 720);

            slow.SetResult(Tiles(Tile("OLD")));
            await slow.Task;

            Assert.Equal(new[] { fresh }, _sut.Monitors);
            Assert.False(_sut.IsLoading);
        }

        [Fact]
        public void Layout_asks_the_workspace_for_the_given_panel_size()
        {
            _sut.RecalculateLayout(640, 480);

            _workspace.Verify(w => w.BuildWorkspaceAsync(640, 480), Times.Once);
        }

        [Theory]
        [InlineData(0, 480)]
        [InlineData(640, 0)]
        [InlineData(-1, -1)]
        public void An_empty_panel_triggers_no_build(double width, double height)
        {
            _sut.RecalculateLayout(width, height);

            Assert.False(_sut.IsLoading);
            _workspace.Verify(w => w.BuildWorkspaceAsync(It.IsAny<double>(), It.IsAny<double>()), Times.Never);
        }

        [Fact]
        public void A_failing_build_is_swallowed_and_stops_the_loading_flag()
        {
            _workspace.Setup(w => w.BuildWorkspaceAsync(It.IsAny<double>(), It.IsAny<double>()))
                      .Returns(Task.FromException<ObservableCollection<MonitorLayoutViewModel>>(new InvalidOperationException("boom")));

            var thrown = Record.Exception(() => _sut.RecalculateLayout(640, 480));

            Assert.Null(thrown);
            Assert.False(_sut.IsLoading);
        }

        [Fact]
        public void Refresh_rescans_with_the_last_panel_size()
        {
            _sut.RecalculateLayout(640, 480);

            _sut.RefreshMonitors(false);

            _workspace.Verify(w => w.RefreshWorkspaceAsync(640, 480), Times.Once);
        }

        [Fact]
        public void Refresh_before_any_layout_does_nothing()
        {
            _sut.RefreshMonitors(true);

            _workspace.Verify(w => w.RefreshWorkspaceAsync(It.IsAny<double>(), It.IsAny<double>()), Times.Never);
        }

        [Fact]
        public void Reload_command_rescans_the_screens()
        {
            _sut.RecalculateLayout(640, 480);

            _sut.ReloadMonitorsCommand.Execute(null);

            _workspace.Verify(w => w.RefreshWorkspaceAsync(640, 480), Times.Once);
        }

        // ---- selection ----

        [Fact]
        public void Selecting_a_tile_moves_the_highlight()
        {
            var a = Tile("A");
            var b = Tile("B", 2);
            Show(a, b);

            _sut.SelectMonitorCommand.Execute(a);
            _sut.SelectMonitorCommand.Execute(b);

            Assert.Same(b, _sut.SelectedMonitor);
            Assert.False(a.IsSelected);
            Assert.True(b.IsSelected);
        }

        [Fact]
        public void Select_command_ignores_anything_but_a_tile()
        {
            var a = Tile("A");
            Show(a);
            _sut.SelectMonitorCommand.Execute(a);

            _sut.SelectMonitorCommand.Execute(42);
            _sut.SelectMonitorCommand.Execute(null);

            Assert.Same(a, _sut.SelectedMonitor);
        }

        [Fact]
        public void A_resize_keeps_the_same_screen_selected_by_hardware_id()
        {
            var before = Tile("A");
            Show(before, Tile("B", 2));
            _sut.SelectMonitorCommand.Execute(before);
            var after = Tile("A");

            Show(Tile("B", 2), after);

            Assert.Same(after, _sut.SelectedMonitor);
            Assert.True(after.IsSelected);
        }

        [Fact]
        public void A_resize_clears_the_selection_when_the_screen_is_gone()
        {
            var a = Tile("A");
            Show(a);
            _sut.SelectMonitorCommand.Execute(a);

            Show(Tile("B", 2));

            Assert.Null(_sut.SelectedMonitor);
        }

        [Fact]
        public void A_rescan_without_preserving_clears_the_selection()
        {
            var a = Tile("A");
            Show(a);
            _sut.SelectMonitorCommand.Execute(a);
            Returns(Tile("A"));

            _sut.RefreshMonitors(false);

            Assert.Null(_sut.SelectedMonitor);
        }

        // ---- dirty tracking, saving and discarding ----

        [Fact]
        public void Editing_a_screen_makes_the_whole_window_dirty()
        {
            var a = Tile("A");
            Show(a);

            a.Configuration.DimLevel = 55;

            Assert.True(_sut.IsDirty);
        }

        [Fact]
        public void Edits_on_a_tile_that_was_replaced_are_ignored()
        {
            var gone = Tile("A");
            Show(gone);
            Show(Tile("B", 2));

            gone.Configuration.DimLevel = 55;

            Assert.False(_sut.IsDirty);
        }

        [Fact]
        public void Dirty_is_announced_once_however_many_edits_follow()
        {
            var a = Tile("A");
            Show(a);
            int raised = 0;
            _sut.PropertyChanged += (_, e) => raised += e.PropertyName == nameof(MainViewModel.IsDirty) ? 1 : 0;

            a.Configuration.DimLevel = 55;
            a.Configuration.DimLevel = 60;
            a.Configuration.IdleValue = 5;

            Assert.Equal(1, raised);
        }

        [Fact]
        public void Saving_successfully_clears_dirty()
        {
            var a = Tile("A");
            Show(a);
            a.Configuration.DimLevel = 55;

            Assert.True(_sut.TrySaveChanges());
            Assert.False(_sut.IsDirty);
            _saver.Verify(s => s.TrySave(_sut.Monitors), Times.Once);
        }

        [Fact]
        public void A_rejected_save_reports_false_and_stays_dirty()
        {
            var a = Tile("A");
            Show(a);
            a.Configuration.DimLevel = 55;
            _saveAccepted = false;

            Assert.False(_sut.TrySaveChanges());
            Assert.True(_sut.IsDirty);
        }

        [Fact]
        public void Discarding_reverts_every_screen_without_rescanning()
        {
            var a = Tile("A");
            Show(a);
            _sut.SelectMonitorCommand.Execute(a);
            double original = a.Configuration.DimLevel;
            a.Configuration.DimLevel = 55;

            _sut.DiscardChanges();

            Assert.Equal(original, a.Configuration.DimLevel);
            Assert.False(_sut.IsDirty);
            Assert.Same(a, _sut.SelectedMonitor);
            _workspace.Verify(w => w.RefreshWorkspaceAsync(It.IsAny<double>(), It.IsAny<double>()), Times.Never);
        }

        // ---- helpers ----

        private void Show(params MonitorLayoutViewModel[] tiles)
        {
            Returns(tiles);
            _sut.RecalculateLayout(640, 480);
        }

        // Both workspace calls complete immediately with the given tiles.
        private void Returns(params MonitorLayoutViewModel[] tiles)
        {
            _workspace.Setup(w => w.BuildWorkspaceAsync(It.IsAny<double>(), It.IsAny<double>())).ReturnsAsync(() => Tiles(tiles));
            _workspace.Setup(w => w.RefreshWorkspaceAsync(It.IsAny<double>(), It.IsAny<double>())).ReturnsAsync(() => Tiles(tiles));
        }

        // The next layout build stays pending until the test completes the returned source.
        private TaskCompletionSource<ObservableCollection<MonitorLayoutViewModel>> Hold()
        {
            var source = new TaskCompletionSource<ObservableCollection<MonitorLayoutViewModel>>();
            _workspace.Setup(w => w.BuildWorkspaceAsync(It.IsAny<double>(), It.IsAny<double>())).Returns(source.Task);
            return source;
        }

        private static ObservableCollection<MonitorLayoutViewModel> Tiles(params MonitorLayoutViewModel[] tiles) => new(tiles);

        private static MonitorLayoutViewModel Tile(string id, int number = 1)
        {
            var area = new Rect(0, 0, 2560, 1440);
            var info = new MonitorInfo { HardwareId = id, DisplayNumber = number, DeviceName = $@"\\.\DISPLAY{number}", Bounds = area };
            return new MonitorLayoutViewModel(info, 0.5, area, 0, 0);
        }
    }
}
