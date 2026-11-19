using OLED_Sleeper.UI.Services.Interfaces;
using System.Diagnostics.CodeAnalysis;
using System.Windows;

namespace OLED_Sleeper.UI.Services
{
    // Plain WPF message boxes. They are parented to the settings window when it is visible; without an
    // owner Windows centres the box on whatever is active, which after a tray-menu click is the menu
    // popup under the mouse pointer.
    [ExcludeFromCodeCoverage]
    public class MessageBoxDialogService(IMainWindowAccessor accessor) : IDialogService
    {
        public MessageBoxResult AskYesNoCancel(string question, string title) =>
            Present(question, title, MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);

        public void ShowError(string text, string title) =>
            Present(text, title, MessageBoxButton.OK, MessageBoxImage.Error);

        private MessageBoxResult Present(string text, string title, MessageBoxButton buttons, MessageBoxImage image)
        {
            // Hidden (e.g. a storage error at startup, before the window ever showed): no owner.
            if (accessor.MainWindow is { IsVisible: true } parent)
                return MessageBox.Show(parent, text, title, buttons, image);

            return MessageBox.Show(text, title, buttons, image);
        }
    }
}
