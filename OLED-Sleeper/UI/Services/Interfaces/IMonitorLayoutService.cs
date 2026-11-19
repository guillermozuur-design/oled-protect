using OLED_Sleeper.Features.MonitorInformation.Models;
using OLED_Sleeper.UI.ViewModels;
using System.Collections.ObjectModel;

namespace OLED_Sleeper.UI.Services.Interfaces
{
    // Turns the desktop's monitor rectangles into scaled tiles that fit a drawing area.
    public interface IMonitorLayoutService
    {
        ObservableCollection<MonitorLayoutViewModel> CreateLayout(List<MonitorInfo> monitors, double areaWidth, double areaHeight);
    }
}
