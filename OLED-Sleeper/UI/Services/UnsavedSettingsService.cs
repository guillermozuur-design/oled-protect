using OLED_Sleeper.UI.Services.Interfaces;
using System.Windows;

namespace OLED_Sleeper.UI.Services
{
    /// <summary>
    /// Prompts through <see cref="IDialogService"/>, saving the settings on Yes and discarding them on No.
    /// </summary>
    public class UnsavedSettingsService(
        IUnsavedSettings unsavedSettings,
        IDialogService dialogService) : IUnsavedSettingsService
    {
        private const string Caption = "Niet opgeslagen wijzigingen";

        /// <inheritdoc />
        public bool ConfirmHide() =>
            Confirm("Je hebt wijzigingen die nog niet zijn opgeslagen. Wil je ze opslaan voordat het venster sluit?");

        /// <inheritdoc />
        public bool ConfirmExit() =>
            Confirm("Je hebt wijzigingen die nog niet zijn opgeslagen. Wil je ze opslaan voordat OLED Space Sleeper stopt?");

        /// <summary>
        /// Prompts only when there is something to lose. Yes answers with whether the save succeeded;
        /// No discards the changes.
        /// </summary>
        private bool Confirm(string question)
        {
            if (!unsavedSettings.IsDirty) return true;

            var result = dialogService.AskYesNoCancel(question, Caption);

            if (result == MessageBoxResult.Cancel) return false;

            if (result == MessageBoxResult.Yes) return unsavedSettings.TrySaveChanges();

            unsavedSettings.DiscardChanges();
            return true;
        }
    }
}
