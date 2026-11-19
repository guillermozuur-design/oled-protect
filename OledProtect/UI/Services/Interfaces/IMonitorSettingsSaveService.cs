using OledProtect.UI.ViewModels;

namespace OledProtect.UI.Services.Interfaces
{
    // Writes the edited monitor configuration to disk, but only when every managed monitor is valid.
    public interface IMonitorSettingsSaveService
    {
        // False (after telling the user why) when validation fails; nothing is written then.
        // On success every monitor is marked clean.
        bool TrySave(IReadOnlyList<MonitorLayoutViewModel> monitors);
    }
}
