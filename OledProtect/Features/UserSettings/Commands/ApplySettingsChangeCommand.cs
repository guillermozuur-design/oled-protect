using OledProtect.Features.UserSettings.Models;
using OledProtect.Messaging.Interfaces;

namespace OledProtect.Features.UserSettings.Commands
{
    // Raised after a save: push the new settings into idle detection and wake the affected screens.
    // Holds only what the caller saved, not the merged file, so each listed monitor gets one restore.
    public sealed class ApplySettingsChangeCommand : ICommand
    {
        public required List<MonitorSettings> Settings { get; init; }
    }
}
