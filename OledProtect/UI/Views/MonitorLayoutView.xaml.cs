using OledProtect.UI.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace OledProtect.UI.Views
{
    // The screen overview. How big the tiles are depends on how much desk there is, so a
    // resize of the desk asks for a fresh arrangement.
    public partial class MonitorLayoutView : UserControl
    {
        public MonitorLayoutView() => InitializeComponent();

        private void OnDeskResized(object sender, SizeChangedEventArgs e)
        {
            var desk = e.NewSize;
            bool hidden = desk.Width <= 0 || desk.Height <= 0;
            if (!hidden && DataContext is MainViewModel screens)
                screens.RecalculateLayout(desk.Width, desk.Height);
        }
    }
}
