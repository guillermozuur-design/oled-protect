using OledProtect.Features.MonitorInformation.Models;
using OledProtect.Tests.TestDoubles;
using OledProtect.UI.ViewModels;
using System.Collections.ObjectModel;
using System.Windows;

namespace OledProtect.Tests.UI.ViewModels
{
    public class MainViewModelTests
    {
        private readonly ScriptedWorkspace _workspace = new();
        private readonly ScriptedSaver _saver = new();
        private readonly ImmediateDispatcher _dispatcher = new();
        private readonly MainViewModel _vm;

        public MainViewModelTests() => _vm = new MainViewModel(_workspace, _saver, _dispatcher);

        // A screen tile as the workspace would hand it out: 1600x900, drawn at a quarter size.
        private static MonitorLayoutViewModel Panel(string hardwareId, int displayNumber = 1)
        {
            var bounds = new Rect(0, 0, 1600, 900);
            var info = new MonitorInfo
            {
                HardwareId = hardwareId,
                DisplayNumber = displayNumber,
                DeviceName = @"\\.\DISPLAY" + displayNumber,
                Bounds = bounds,
            };
            return new MonitorLayoutViewModel(info, 0.25, bounds, 0, 0);
        }

        // Lets the workspace hand out these tiles and lays them out in a 900x500 panel.
        private void Present(params MonitorLayoutViewModel[] screens)
        {
            _workspace.Screens = screens;
            _vm.RecalculateLayout(900, 500);
        }

        private List<string> WatchNotifications()
        {
            var names = new List<string>();
            _vm.PropertyChanged += (_, e) => names.Add(e.PropertyName ?? "");
            return names;
        }

        // ===== Layout =====

        [Fact]
        public void The_overview_lists_exactly_the_tiles_it_was_given()
        {
            var left = Panel("L");
            var right = Panel("R", 2);

            Present(left, right);

            Assert.Equal(new[] { left, right }, _vm.Monitors);
            Assert.False(_vm.IsLoading);
        }

        [Fact]
        public void Laying_out_again_drops_the_previous_tiles()
        {
            Present(Panel("L"), Panel("R", 2));
            var only = Panel("X", 3);

            Present(only);

            Assert.Equal(new[] { only }, _vm.Monitors);
        }

        [Fact]
        public void The_workspace_is_asked_for_the_panel_size()
        {
            _vm.RecalculateLayout(1234, 567);

            Assert.Equal(new[] { "build 1234x567" }, _workspace.Log);
        }

        [Theory]
        [InlineData(0, 500)]
        [InlineData(900, 0)]
        [InlineData(-5, 500)]
        [InlineData(0, 0)]
        public void A_panel_without_area_is_not_laid_out(double width, double height)
        {
            _vm.RecalculateLayout(width, height);

            Assert.Empty(_workspace.Log);
            Assert.False(_vm.IsLoading);
        }

        [Fact]
        public void From_a_worker_thread_the_tiles_are_handed_to_the_dispatcher_once()
        {
            _dispatcher.IsOnUiThread = false;
            var screen = Panel("L");

            Present(screen);

            Assert.Equal((1, 0), (_dispatcher.InvokeCount, _dispatcher.InvokeAsyncCount));
            Assert.Equal(new[] { screen }, _vm.Monitors);
        }

        [Fact]
        public void On_the_ui_thread_the_dispatcher_stays_unused()
        {
            Present(Panel("L"));

            Assert.Equal(0, _dispatcher.InvokeCount + _dispatcher.InvokeAsyncCount);
            Assert.Single(_vm.Monitors);
        }

        [Fact]
        public async Task IsLoading_covers_exactly_the_time_the_build_is_running()
        {
            var stalled = _workspace.Stall();

            _vm.RecalculateLayout(900, 500);
            Assert.True(_vm.IsLoading);

            stalled.SetResult(new ObservableCollection<MonitorLayoutViewModel> { Panel("L") });
            await stalled.Task;

            Assert.False(_vm.IsLoading);
            Assert.Single(_vm.Monitors);
        }

        [Fact]
        public async Task An_outdated_build_finishing_late_cannot_overwrite_the_newer_one()
        {
            var stalled = _workspace.Stall();
            _vm.RecalculateLayout(900, 500);
            var current = Panel("NEW");
            Present(current);

            stalled.SetResult(new ObservableCollection<MonitorLayoutViewModel> { Panel("OUTDATED") });
            await stalled.Task;

            Assert.Equal(new[] { current }, _vm.Monitors);
            Assert.False(_vm.IsLoading);
        }

        [Fact]
        public void A_crashing_build_is_contained_and_ends_the_loading_state()
        {
            _workspace.Fail(new InvalidOperationException("scan crashed"));

            var escaped = Record.Exception(() => _vm.RecalculateLayout(900, 500));

            Assert.Null(escaped);
            Assert.False(_vm.IsLoading);
            Assert.Empty(_vm.Monitors);
        }

        // ===== Rescanning =====

        [Fact]
        public void A_rescan_reuses_the_last_panel_size()
        {
            _vm.RecalculateLayout(1111, 222);

            _vm.RefreshMonitors(preserveSelection: false);

            Assert.Equal(new[] { "build 1111x222", "refresh 1111x222" }, _workspace.Log);
        }

        [Fact]
        public void Before_the_first_layout_a_rescan_has_no_size_and_is_skipped()
        {
            _vm.RefreshMonitors(preserveSelection: true);

            Assert.Empty(_workspace.Log);
        }

        [Fact]
        public void The_reload_command_triggers_a_rescan()
        {
            _vm.RecalculateLayout(1111, 222);

            _vm.ReloadMonitorsCommand.Execute(null);

            Assert.Equal("refresh 1111x222", _workspace.Log[^1]);
        }

        // ===== Selection =====

        [Fact]
        public void Only_the_last_clicked_tile_is_highlighted()
        {
            var left = Panel("L");
            var right = Panel("R", 2);
            Present(left, right);

            _vm.SelectMonitorCommand.Execute(left);
            _vm.SelectMonitorCommand.Execute(right);

            Assert.Same(right, _vm.SelectedMonitor);
            Assert.Equal((false, true), (left.IsSelected, right.IsSelected));
        }

        [Theory]
        [InlineData(null)]
        [InlineData(7)]
        [InlineData("L")]
        public void Clicking_with_something_other_than_a_tile_changes_nothing(object? parameter)
        {
            var left = Panel("L");
            Present(left);
            _vm.SelectMonitorCommand.Execute(left);

            _vm.SelectMonitorCommand.Execute(parameter);

            Assert.Same(left, _vm.SelectedMonitor);
        }

        [Fact]
        public void After_a_resize_the_same_screen_is_selected_again()
        {
            var before = Panel("L");
            Present(Panel("R", 2), before);
            _vm.SelectMonitorCommand.Execute(before);
            var after = Panel("L");

            Present(after, Panel("R", 2));

            Assert.Same(after, _vm.SelectedMonitor);
            Assert.True(after.IsSelected);
        }

        [Fact]
        public void After_a_resize_without_that_screen_nothing_is_selected()
        {
            var gone = Panel("L");
            Present(gone);
            _vm.SelectMonitorCommand.Execute(gone);

            Present(Panel("R", 2));

            Assert.Null(_vm.SelectedMonitor);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(false, false)]
        public void A_rescan_keeps_the_selection_only_when_asked(bool preserve, bool stillSelected)
        {
            var first = Panel("L");
            Present(first);
            _vm.SelectMonitorCommand.Execute(first);
            var rescanned = Panel("L");
            _workspace.Screens = [rescanned];

            _vm.RefreshMonitors(preserve);

            Assert.Equal(stillSelected, ReferenceEquals(_vm.SelectedMonitor, rescanned));
            Assert.Equal(stillSelected, _vm.SelectedMonitor is not null);
        }

        // ===== Pending edits =====

        [Fact]
        public void Fresh_tiles_leave_the_window_clean()
        {
            Present(Panel("L"), Panel("R", 2));

            Assert.False(_vm.IsDirty);
        }

        [Fact]
        public void An_edit_on_any_tile_marks_the_window_dirty()
        {
            var right = Panel("R", 2);
            Present(Panel("L"), right);

            right.Configuration.DimLevel = 42;

            Assert.True(_vm.IsDirty);
        }

        [Fact]
        public void A_tile_that_left_the_overview_no_longer_counts()
        {
            var old = Panel("L");
            Present(old);
            Present(Panel("R", 2));

            old.Configuration.DimLevel = 42;

            Assert.False(_vm.IsDirty);
        }

        [Fact]
        public void A_burst_of_edits_announces_dirty_a_single_time()
        {
            var screen = Panel("L");
            Present(screen);
            var notifications = WatchNotifications();

            screen.Configuration.DimLevel = 42;
            screen.Configuration.IdleValue = 3;
            screen.Configuration.DimLevel = 43;

            Assert.Single(notifications, name => name == nameof(MainViewModel.IsDirty));
        }

        [Fact]
        public void An_accepted_save_hands_over_the_tiles_and_clears_dirty()
        {
            var screen = Panel("L");
            Present(screen);
            screen.Configuration.DimLevel = 42;

            bool saved = _vm.TrySaveChanges();

            Assert.True(saved);
            Assert.False(_vm.IsDirty);
            Assert.Same(_vm.Monitors, Assert.Single(_saver.Received));
        }

        [Fact]
        public void A_refused_save_reports_false_and_keeps_the_edits()
        {
            var screen = Panel("L");
            Present(screen);
            screen.Configuration.DimLevel = 42;
            _saver.Accepts = false;

            Assert.False(_vm.TrySaveChanges());
            Assert.True(_vm.IsDirty);
            Assert.Equal(42, screen.Configuration.DimLevel);
        }

        [Fact]
        public void Discarding_restores_the_saved_values_in_place()
        {
            var screen = Panel("L");
            Present(screen);
            _vm.SelectMonitorCommand.Execute(screen);
            double stored = screen.Configuration.DimLevel;
            screen.Configuration.DimLevel = stored + 17;
            int callsBefore = _workspace.Log.Count;

            _vm.DiscardChanges();

            Assert.Equal(stored, screen.Configuration.DimLevel);
            Assert.False(_vm.IsDirty);
            Assert.Same(screen, _vm.SelectedMonitor);
            Assert.Equal(callsBefore, _workspace.Log.Count);
        }
    }
}
