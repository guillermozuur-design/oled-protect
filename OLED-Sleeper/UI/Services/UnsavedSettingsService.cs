using OLED_Sleeper.UI.Services.Interfaces;
using System.Windows;

namespace OLED_Sleeper.UI.Services
{
    // Yes = save (and go on only if the save was accepted), No = throw the edits away and go on,
    // Cancel = stay. Without pending edits there is nothing to ask.
    public class UnsavedSettingsService(
        IUnsavedSettings edits,
        IDialogService dialogs) : IUnsavedSettingsService
    {
        private const string Title = "Niet opgeslagen wijzigingen";
        private const string Question = "Je hebt wijzigingen die nog niet zijn opgeslagen. Wil je ze opslaan voordat {0}?";

        public bool ConfirmHide() => Resolve("het venster sluit");

        public bool ConfirmExit() => Resolve("OLED Space Sleeper stopt");

        private bool Resolve(string whatHappensNext)
        {
            if (!edits.IsDirty)
                return true;

            var answer = dialogs.AskYesNoCancel(string.Format(Question, whatHappensNext), Title);
            switch (answer)
            {
                case MessageBoxResult.Yes:
                    return edits.TrySaveChanges();
                case MessageBoxResult.Cancel:
                    return false;
                default:
                    edits.DiscardChanges();
                    return true;
            }
        }
    }
}
