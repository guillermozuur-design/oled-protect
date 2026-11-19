using OledProtect.Features.MonitorBehavior.Models;
using OledProtect.Features.MonitorInformation.Models;
using OledProtect.Features.UserSettings.Models;
using OledProtect.UI.Models;
using OledProtect.UI.ViewModels;
using System.Windows;

namespace OledProtect.Tests.UI.Helpers
{
    /// <summary>
    /// Fluent factory for settings-window tiles. A tile starts as the app would show a never-configured
    /// screen; <see cref="ReadyToSave"/> gives it a complete, stored configuration, and every
    /// <see cref="Then"/> step is applied afterwards as an unsaved edit.
    /// </summary>
    internal sealed class ScreenTileBuilder
    {
        private readonly int _displayNumber;
        private readonly List<Action<MonitorConfigurationViewModel>> _edits = [];
        private bool _stored;

        private ScreenTileBuilder(int displayNumber) => _displayNumber = displayNumber;

        public static ScreenTileBuilder Screen(int displayNumber) => new(displayNumber);

        public string HardwareId => $"PANEL-{_displayNumber:00}";

        public ScreenTileBuilder ReadyToSave()
        {
            _stored = true;
            return this;
        }

        public ScreenTileBuilder Watched() => Then(c => c.IsManaged = true);

        public ScreenTileBuilder Then(Action<MonitorConfigurationViewModel> edit)
        {
            _edits.Add(edit);
            return this;
        }

        public MonitorLayoutViewModel Build()
        {
            var area = new Rect(2560 * (_displayNumber - 1), 0, 2560, 1440);
            var info = new MonitorInfo
            {
                HardwareId = HardwareId,
                DisplayNumber = _displayNumber,
                DeviceName = @"\\.\DISPLAY" + _displayNumber,
                Bounds = area,
            };
            var tile = new MonitorLayoutViewModel(info, 0.1, area, 0, 0);

            if (_stored)
            {
                tile.Configuration.ApplySettings(new MonitorSettings
                {
                    Behavior = MonitorBehaviorType.Blackout,
                    IdleUnit = TimeUnit.Seconds,
                    IdleValue = 90,
                    IsActiveOnActiveWindow = true,
                    IsManaged = true,
                    HardwareId = HardwareId,
                });
                tile.Configuration.MarkAsSaved();
            }

            _edits.ForEach(edit => edit(tile.Configuration));
            return tile;
        }

        public static implicit operator MonitorLayoutViewModel(ScreenTileBuilder builder) => builder.Build();
    }
}
