using OledProtect.UI.Services.Interfaces;
using System.Windows;

namespace OledProtect.UI.Services
{
    /// <summary>
    /// Asks what to do with unsaved edits before the window closes or the app stops. Only asks
    /// when there is something to lose. Yes saves (and only lets go once that worked), No drops
    /// the edits and lets go, Cancel stays in the settings.
    /// </summary>
    public class UnsavedSettingsService(
        IUnsavedSettings edits,
        IDialogService dialogs) : IUnsavedSettingsService
    {
        private const string Caption = "Even checken";

        public bool ConfirmHide() => MayLeave("het venster dichtgaat");

        public bool ConfirmExit() => MayLeave("OLED Protect afsluit");

        // The whole question, including what each button does, since a message box says only Ja/Nee.
        private static string Question(string moment) =>
            "Je aanpassingen zijn nog niet bewaard." + Environment.NewLine + Environment.NewLine +
            $"Eerst bewaren, voordat {moment}?" + Environment.NewLine +
            "Ja bewaart ze, Nee laat ze vallen, Annuleren brengt je terug.";

        private bool MayLeave(string moment)
        {
            if (!edits.IsDirty)
                return true;

            return dialogs.AskYesNoCancel(Question(moment), Caption) switch
            {
                MessageBoxResult.Yes => edits.TrySaveChanges(),
                MessageBoxResult.Cancel => false,
                _ => DiscardAndGo(),
            };
        }

        private bool DiscardAndGo()
        {
            edits.DiscardChanges();
            return true;
        }
    }
}
