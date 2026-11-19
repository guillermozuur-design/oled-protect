using System.Windows;

namespace OLED_Sleeper.UI.Services.Interfaces
{
    // Modal prompts. Callers block until the user answers, so only call these from the UI thread.
    public interface IDialogService
    {
        // A warning with Yes / No / Cancel; returns the button that was pressed.
        MessageBoxResult AskYesNoCancel(string question, string title);

        // An error with just an OK button.
        void ShowError(string text, string title);
    }
}
