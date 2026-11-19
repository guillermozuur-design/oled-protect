using System.Windows.Controls;

namespace OledProtect.UI.Views
{
    /// <summary>
    /// Per-screen settings panel. Pure data binding against <see cref="ViewModels.MonitorConfigurationViewModel"/>;
    /// there is no logic in the code-behind.
    /// </summary>
    public partial class MonitorConfigurationView : UserControl
    {
        public MonitorConfigurationView() => InitializeComponent();
    }
}
