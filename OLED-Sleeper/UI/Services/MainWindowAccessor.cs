using OLED_Sleeper.UI.Services.Interfaces;
using System.Diagnostics.CodeAnalysis;
using System.Windows;

namespace OLED_Sleeper.UI.Services
{
    // Thin wrapper; Application.Current is null outside a running WPF app (unit tests), hence the check.
    [ExcludeFromCodeCoverage]
    public class MainWindowAccessor : IMainWindowAccessor
    {
        public Window? MainWindow => Application.Current is { } app ? app.MainWindow : null;

        public void SetMainWindow(Window shell) => Application.Current.MainWindow = shell;
    }
}
