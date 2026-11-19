using OledProtect.UI.Services;
using OledProtect.UI.Services.Interfaces;
using System.Windows;

namespace OledProtect.Tests.UI.Services
{
    public class UnsavedSettingsServiceTests
    {
        // One stand-in plays both the pending edits and the user at the dialog, and writes down
        // every step in a single journal, so the tests can check what happened and in which order.
        private sealed class Session : IUnsavedSettings, IDialogService
        {
            public bool HasEdits { get; init; } = true;
            public MessageBoxResult Reply { get; init; } = MessageBoxResult.Cancel;
            public bool SaveSucceeds { get; init; } = true;
            public List<string> Journal { get; } = [];
            public List<string> Questions { get; } = [];

            bool IUnsavedSettings.IsDirty => HasEdits;

            bool IUnsavedSettings.TrySaveChanges()
            {
                Journal.Add(SaveSucceeds ? "saved" : "save refused");
                return SaveSucceeds;
            }

            void IUnsavedSettings.DiscardChanges() => Journal.Add("discarded");

            MessageBoxResult IDialogService.AskYesNoCancel(string question, string title)
            {
                Journal.Add("asked");
                Questions.Add(title + " | " + question);
                return Reply;
            }

            void IDialogService.ShowError(string text, string title) => Journal.Add("error shown");

            public bool Run(string leaving)
            {
                var guard = new UnsavedSettingsService(this, this);
                return leaving switch
                {
                    "window" => guard.ConfirmHide(),
                    "app" => guard.ConfirmExit(),
                    _ => throw new ArgumentOutOfRangeException(nameof(leaving)),
                };
            }
        }

        [Theory]
        [InlineData("window")]
        [InlineData("app")]
        public void Nothing_pending_means_no_dialog_and_go(string leaving)
        {
            var session = new Session { HasEdits = false };

            Assert.True(session.Run(leaving));
            Assert.Empty(session.Journal);
        }

        // Decision table: what the user answers, whether the save goes through, and the outcome.
        [Theory]
        [InlineData("window", MessageBoxResult.Yes, true, true, "asked,saved")]
        [InlineData("app", MessageBoxResult.Yes, true, true, "asked,saved")]
        [InlineData("window", MessageBoxResult.Yes, false, false, "asked,save refused")]
        [InlineData("app", MessageBoxResult.Yes, false, false, "asked,save refused")]
        [InlineData("window", MessageBoxResult.No, true, true, "asked,discarded")]
        [InlineData("app", MessageBoxResult.No, true, true, "asked,discarded")]
        [InlineData("window", MessageBoxResult.Cancel, true, false, "asked")]
        [InlineData("app", MessageBoxResult.Cancel, true, false, "asked")]
        public void The_answer_decides_what_happens_to_the_edits(
            string leaving, MessageBoxResult reply, bool saveSucceeds, bool mayGo, string steps)
        {
            var session = new Session { Reply = reply, SaveSucceeds = saveSucceeds };

            bool result = session.Run(leaving);

            Assert.Equal(mayGo, result);
            Assert.Equal(steps, string.Join(",", session.Journal));
        }

        [Theory]
        [InlineData("window", "voordat het venster sluit")]
        [InlineData("app", "voordat OLED Protect stopt")]
        public void The_dialog_says_what_is_about_to_happen(string leaving, string phrase)
        {
            var session = new Session { Reply = MessageBoxResult.No };

            session.Run(leaving);

            string asked = Assert.Single(session.Questions);
            Assert.StartsWith("Niet opgeslagen wijzigingen | ", asked);
            Assert.Contains(phrase, asked);
        }
    }
}
