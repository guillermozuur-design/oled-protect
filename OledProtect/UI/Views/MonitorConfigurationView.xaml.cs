using System.Windows.Controls;

namespace OledProtect.UI.Views
{
    // The settings of one screen. Everything runs through bindings to
    // MonitorConfigurationViewModel, so this file only loads the XAML.
    public partial class MonitorConfigurationView : UserControl
    {
        public MonitorConfigurationView() => InitializeComponent();
    }
}
