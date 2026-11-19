using OLED_Sleeper.Features.MonitorInformation.Models;
using OLED_Sleeper.UI.Helpers;
using System.Windows;
using System.Windows.Media;

namespace OLED_Sleeper.UI.ViewModels
{
    /// <summary>
    /// One screen as a clickable tile in the "Jouw schermen" overview, scaled down to fit the panel,
    /// together with the settings that belong to it.
    /// </summary>
    public class MonitorLayoutViewModel : ViewModelBase
    {
        // Shared and frozen: every tile looks the same, and a frozen brush is safe across threads.
        private static readonly Brush NightSkyTile = MakeNightSkyBrush();

        private readonly MonitorInfo _info;
        private bool _highlighted;

        /// <param name="monitor">The physical screen.</param>
        /// <param name="scale">Panel pixels per desktop pixel.</param>
        /// <param name="totalBounds">The desktop rectangle that holds every screen.</param>
        /// <param name="offsetX">Horizontal padding that centres the scaled desktop in the panel.</param>
        /// <param name="offsetY">Vertical padding that centres the scaled desktop in the panel.</param>
        public MonitorLayoutViewModel(MonitorInfo monitor, double scale, Rect totalBounds, double offsetX, double offsetY)
        {
            _info = monitor;

            var area = monitor.Bounds;
            ScaledLeft = offsetX + scale * (area.Left - totalBounds.Left);
            ScaledTop = offsetY + scale * (area.Top - totalBounds.Top);
            ScaledWidth = scale * area.Width;
            ScaledHeight = scale * area.Height;

            Configuration = new(monitor);
            Configuration.PropertyChanged += RelayDirtyFlag;
        }

        public MonitorConfigurationViewModel Configuration { get; }

        public string HardwareId => _info.HardwareId;

        public int DisplayNumber => _info.DisplayNumber;

        public string MonitorTitle => MonitorTitleFormatter.Format(_info);

        public string ResolutionText => FormattableString.Invariant($"{(int)_info.Bounds.Width}x{(int)_info.Bounds.Height}");

        public Brush BackgroundColor => NightSkyTile;

        public double ScaledLeft { get; }

        public double ScaledTop { get; }

        public double ScaledWidth { get; }

        public double ScaledHeight { get; }

        public bool IsSelected
        {
            get => _highlighted;
            set => SetField(ref _highlighted, value);
        }

        /// <summary>True while this screen's settings differ from what is stored.</summary>
        public bool IsDirty => Configuration is { IsDirty: true };

        private void RelayDirtyFlag(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MonitorConfigurationViewModel.IsDirty)) OnPropertyChanged(nameof(IsDirty));
        }

        private static Brush MakeNightSkyBrush()
        {
            var brush = new LinearGradientBrush(Color.FromRgb(30, 38, 62), Color.FromRgb(14, 17, 28), 90);
            brush.Freeze();
            return brush;
        }
    }
}
