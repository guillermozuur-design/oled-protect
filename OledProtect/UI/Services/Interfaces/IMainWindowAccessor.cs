using System.Windows;

namespace OledProtect.UI.Services.Interfaces
{
    // Indirection over Application.MainWindow so services can be tested without a WPF application.
    public interface IMainWindowAccessor
    {
        // Null until SetMainWindow has run. UI thread only.
        Window? MainWindow { get; }

        void SetMainWindow(Window shell);
    }
}
