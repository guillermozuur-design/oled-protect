using OledProtect.UI.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace OledProtect.UI.Views
{
    /// <summary>
    /// The screen overview. The tile positions depend on the space available, so every resize
    /// asks the view model for a fresh layout.
    /// </summary>
    public partial class MonitorLayoutView : UserControl
    {
        public MonitorLayoutView() => InitializeComponent();

        private void OnPanelResized(object sender, SizeChangedEventArgs e)
        {
            // A collapsed panel reports zero height; there is nothing to lay out then.
            if (e.NewSize.Height <= 0 || DataContext is not MainViewModel main) return;

            main.RecalculateLayout(e.NewSize.Width, e.NewSize.Height);
        }
    }
}
