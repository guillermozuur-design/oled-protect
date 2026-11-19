using OledProtect.Features.MonitorInformation.Models;
using OledProtect.UI.ViewModels;
using System.Collections.ObjectModel;

namespace OledProtect.UI.Services.Interfaces
{
    // Turns the desktop's monitor rectangles into scaled tiles that fit a drawing area.
    public interface IMonitorLayoutService
    {
        ObservableCollection<MonitorLayoutViewModel> CreateLayout(List<MonitorInfo> monitors, double areaWidth, double areaHeight);
    }
}
