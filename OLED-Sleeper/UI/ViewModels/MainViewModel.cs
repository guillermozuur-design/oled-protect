using OLED_Sleeper.Infrastructure.Runtime.Interfaces;
using OLED_Sleeper.UI.Commands;
using OLED_Sleeper.UI.Services.Interfaces;
using Serilog;
using System.Collections.ObjectModel;
using System.ComponentModel;
using ICommand = System.Windows.Input.ICommand;

namespace OLED_Sleeper.UI.ViewModels
{
    /// <summary>
    /// Backs the settings window: the screen overview, which screen is being edited, and whether
    /// any screen has edits that still have to be written (the window auto-saves them).
    /// </summary>
    public class MainViewModel : ViewModelBase, IUnsavedSettings
    {
        private delegate Task<ObservableCollection<MonitorLayoutViewModel>> LayoutSource(double width, double height);

        private readonly IWorkspaceService _workspace;
        private readonly IMonitorSettingsSaveService _saver;
        private readonly IDispatcher _ui;

        private Size _panel;
        private MonitorLayoutViewModel? _selected;
        private bool _anyUnsaved;
        private bool _busy;

        // Bumped on every layout request; a request that finishes after a newer one started is ignored,
        // so a slow monitor scan cannot overwrite a newer layout.
        private int _latestRequest;

        public MainViewModel(IWorkspaceService workspaceService, IMonitorSettingsSaveService saveService, IDispatcher dispatcher)
        {
            _workspace = workspaceService;
            _saver = saveService;
            _ui = dispatcher;

            SelectMonitorCommand = new RelayCommand(p =>
            {
                if (p is MonitorLayoutViewModel tile) SelectedMonitor = tile;
            });
            ReloadMonitorsCommand = new RelayCommand(() => RefreshMonitors(preserveSelection: false));
        }

        public ObservableCollection<MonitorLayoutViewModel> Monitors { get; } = [];

        /// <summary>Clicking a tile: the parameter is the tile's <see cref="MonitorLayoutViewModel"/>.</summary>
        public ICommand SelectMonitorCommand { get; }

        /// <summary>Re-detects the connected screens and redraws the overview.</summary>
        public ICommand ReloadMonitorsCommand { get; }

        public MonitorLayoutViewModel? SelectedMonitor
        {
            get => _selected;
            private set
            {
                var previous = _selected;
                if (!SetField(ref _selected, value)) return;

                if (previous is not null) previous.IsSelected = false;
                if (value is not null) value.IsSelected = true;
            }
        }

        public bool IsDirty
        {
            get => _anyUnsaved;
            private set => SetField(ref _anyUnsaved, value);
        }

        public bool IsLoading
        {
            get => _busy;
            private set => SetField(ref _busy, value);
        }

        /// <summary>Rescans the screens, then lays them out in the last known panel size.</summary>
        /// <param name="preserveSelection">Keep the same screen selected if it is still there.</param>
        public void RefreshMonitors(bool preserveSelection) =>
            Rebuild(_workspace.RefreshWorkspaceAsync, _panel, preserveSelection);

        /// <summary>Lays the known screens out again for a resized panel, keeping the selection.</summary>
        public void RecalculateLayout(double width, double height) =>
            Rebuild(_workspace.BuildWorkspaceAsync, new Size(width, height), preserveSelection: true);

        public bool TrySaveChanges()
        {
            bool written = _saver.TrySave(Monitors);
            if (written) UpdateDirtyFlag();
            return written;
        }

        public void DiscardChanges()
        {
            foreach (var tile in Monitors) tile.Configuration.Revert();
            UpdateDirtyFlag();
        }

        // Fire-and-forget by design: callers are UI events. Every failure is caught and logged here.
        private async void Rebuild(LayoutSource source, Size panel, bool preserveSelection)
        {
            if (!panel.HasArea) return;

            _panel = panel;
            int request = ++_latestRequest;
            string? keepSelected = preserveSelection ? _selected?.HardwareId : null;
            IsLoading = true;

            ObservableCollection<MonitorLayoutViewModel> tiles;
            try
            {
                tiles = await source(panel.Width, panel.Height);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not build the screen overview.");
                if (request == _latestRequest) IsLoading = false;
                return;
            }

            if (request != _latestRequest) return;

            ReplaceTiles(tiles);
            SelectedMonitor = keepSelected is null ? null : Monitors.FirstOrDefault(t => t.HardwareId == keepSelected);
            UpdateDirtyFlag();
            IsLoading = false;
        }

        private void ReplaceTiles(IEnumerable<MonitorLayoutViewModel> tiles)
        {
            // The collection is bound to the view, so it may only change on the UI thread.
            if (!_ui.IsOnUiThread)
            {
                _ui.Invoke(() => ReplaceTiles(tiles));
                return;
            }

            // Unhook old tiles so a discarded tile can no longer flip the dirty flag.
            foreach (var old in Monitors) old.PropertyChanged -= OnTileChanged;
            Monitors.Clear();

            foreach (var tile in tiles)
            {
                tile.PropertyChanged += OnTileChanged;
                Monitors.Add(tile);
            }
        }

        private void OnTileChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MonitorLayoutViewModel.IsDirty)) UpdateDirtyFlag();
        }

        private void UpdateDirtyFlag() => IsDirty = Monitors.Any(t => t.IsDirty);

        private readonly record struct Size(double Width, double Height)
        {
            public bool HasArea => Width > 0 && Height > 0;
        }
    }
}
