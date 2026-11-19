using OLED_Sleeper.UI.Services;
using OLED_Sleeper.UI.Services.Interfaces;
using System.Windows;

namespace OLED_Sleeper.Tests.UI.Services
{
    public class UnsavedSettingsServiceTests
    {
        private readonly PendingEdits _edits = new() { IsDirty = true, SaveAccepted = true };
        private readonly ScriptedDialogs _dialogs = new();
        private readonly UnsavedSettingsService _sut;

        public UnsavedSettingsServiceTests()
        {
            _sut = new UnsavedSettingsService(_edits, _dialogs);
        }

        public static TheoryData<string> Actions => new() { "hide", "exit" };

        private bool Confirm(string action) => action == "hide" ? _sut.ConfirmHide() : _sut.ConfirmExit();

        [Theory, MemberData(nameof(Actions))]
        public void Without_pending_edits_nothing_is_asked(string action)
        {
            _edits.IsDirty = false;

            Assert.True(Confirm(action));
            Assert.Empty(_dialogs.Asked);
            Assert.Equal(0, _edits.Saves);
            Assert.Equal(0, _edits.Discards);
        }

        [Theory, MemberData(nameof(Actions))]
        public void Cancel_stops_and_keeps_the_edits_pending(string action)
        {
            _dialogs.Answer = MessageBoxResult.Cancel;

            Assert.False(Confirm(action));
            Assert.Equal(0, _edits.Saves);
            Assert.Equal(0, _edits.Discards);
        }

        [Theory, MemberData(nameof(Actions))]
        public void Yes_saves_and_goes_on(string action)
        {
            _dialogs.Answer = MessageBoxResult.Yes;

            Assert.True(Confirm(action));
            Assert.Equal(1, _edits.Saves);
            Assert.Equal(0, _edits.Discards);
        }

        [Theory, MemberData(nameof(Actions))]
        public void Yes_with_a_rejected_save_stops(string action)
        {
            _dialogs.Answer = MessageBoxResult.Yes;
            _edits.SaveAccepted = false;

            Assert.False(Confirm(action));
            Assert.Equal(0, _edits.Discards);
        }

        [Theory, MemberData(nameof(Actions))]
        public void No_discards_and_goes_on(string action)
        {
            _dialogs.Answer = MessageBoxResult.No;

            Assert.True(Confirm(action));
            Assert.Equal(1, _edits.Discards);
            Assert.Equal(0, _edits.Saves);
        }

        [Fact]
        public void The_question_names_what_is_about_to_happen()
        {
            _dialogs.Answer = MessageBoxResult.No;

            _sut.ConfirmHide();
            _sut.ConfirmExit();

            Assert.Collection(_dialogs.Asked,
                hide =>
                {
                    Assert.Contains("voordat het venster sluit", hide.Question);
                    Assert.Equal("Niet opgeslagen wijzigingen", hide.Title);
                },
                exit =>
                {
                    Assert.Contains("voordat OLED Space Sleeper stopt", exit.Question);
                    Assert.Equal("Niet opgeslagen wijzigingen", exit.Title);
                });
        }

        private sealed class PendingEdits : IUnsavedSettings
        {
            public bool IsDirty { get; set; }
            public bool SaveAccepted { get; set; }
            public int Saves { get; private set; }
            public int Discards { get; private set; }

            public bool TrySaveChanges()
            {
                Saves++;
                return SaveAccepted;
            }

            public void DiscardChanges() => Discards++;
        }

        private sealed class ScriptedDialogs : IDialogService
        {
            public MessageBoxResult Answer { get; set; } = MessageBoxResult.Cancel;
            public List<(string Question, string Title)> Asked { get; } = new();

            public MessageBoxResult AskYesNoCancel(string question, string title)
            {
                Asked.Add((question, title));
                return Answer;
            }

            public void ShowError(string text, string title) =>
                throw new InvalidOperationException("No error dialog expected here.");
        }
    }
}
