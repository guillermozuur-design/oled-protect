using OledProtect.UI.ViewModels;
using System.Collections.ObjectModel;

namespace OledProtect.UI.Services.Interfaces
{
    // Produces the monitor tiles shown in the settings window, with their saved configuration applied.
    public interface IWorkspaceService
    {
        // Uses the monitor list the app already knows about.
        Task<ObservableCollection<MonitorLayoutViewModel>> BuildWorkspaceAsync(double canvasWidth, double canvasHeight);

        // Asks Windows for the monitors again first (e.g. after a display was plugged in).
        Task<ObservableCollection<MonitorLayoutViewModel>> RefreshWorkspaceAsync(double canvasWidth, double canvasHeight);
    }
}
