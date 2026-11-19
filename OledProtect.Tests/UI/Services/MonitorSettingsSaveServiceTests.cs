using OledProtect.Features.UserSettings.Models;
using OledProtect.Features.UserSettings.Services.Interfaces;
using OledProtect.UI.Services;
using OledProtect.UI.Services.Interfaces;
using OledProtect.UI.ViewModels;
using System.IO;
using System.Windows;
using static OledProtect.Tests.UI.Helpers.ScreenTileBuilder;

namespace OledProtect.Tests.UI.Services
{
    public class MonitorSettingsSaveServiceTests
    {
        private readonly SettingsFileSpy _disk = new();
        private readonly ErrorDialogSpy _dialogs = new();

        private bool Save(params MonitorLayoutViewModel[] tiles) =>
            new MonitorSettingsSaveService(_disk, _dialogs).TrySave(tiles);

        [Fact]
        public void Complete_screens_go_to_disk_in_a_single_write()
        {
            Assert.True(Save(Screen(1).ReadyToSave().Then(c => c.DimLevel = 35), Screen(2).ReadyToSave()));

            var batch = Assert.Single(_disk.Writes);
            Assert.Equal(new[] { "PANEL-01", "PANEL-02" }, batch.Select(s => s.HardwareId));
            Assert.Equal(35, batch[0].DimLevel);
            Assert.Equal(90, batch[1].IdleValue);
            Assert.Empty(_dialogs.Errors);
        }

        [Fact]
        public void After_a_write_no_screen_has_pending_edits()
        {
            MonitorLayoutViewModel edited = Screen(1).ReadyToSave().Then(c => c.DimLevel = 35);
            MonitorLayoutViewModel untouched = Screen(2).ReadyToSave();
            Assert.True(edited.IsDirty);

            Save(edited, untouched);

            Assert.False(edited.IsDirty || untouched.IsDirty);
        }

        [Fact]
        public void A_faulty_watched_screen_stops_the_write_and_tells_the_user()
        {
            MonitorLayoutViewModel faulty = Screen(3).ReadyToSave().Then(c => c.IdleValue = 0);

            Assert.False(Save(Screen(1).ReadyToSave(), faulty));

            Assert.Empty(_disk.Writes);
            var (text, title) = Assert.Single(_dialogs.Errors);
            Assert.Equal("Instellingen niet compleet", title);
            Assert.Contains("Scherm 3", text);
            Assert.True(faulty.IsDirty);
        }

        [Fact]
        public void A_faulty_screen_that_is_not_watched_is_still_written()
        {
            var ignored = Screen(1).ReadyToSave().Then(c => c.IdleValue = 0).Then(c => c.IsManaged = false);

            Assert.True(Save(ignored));

            Assert.Single(_disk.Writes);
            Assert.Empty(_dialogs.Errors);
        }

        [Fact]
        public void No_screens_still_produce_one_empty_write()
        {
            Assert.True(Save());

            Assert.Empty(Assert.Single(_disk.Writes));
        }

        [Fact]
        public void A_write_that_blows_up_leaves_the_edits_pending()
        {
            _disk.Failure = new IOException("disk gone");
            MonitorLayoutViewModel edited = Screen(1).ReadyToSave().Then(c => c.DimLevel = 35);

            Assert.Throws<IOException>(() => Save(edited));

            Assert.True(edited.IsDirty);
        }

        private sealed class SettingsFileSpy : IMonitorSettingsFileService
        {
            public List<List<MonitorSettings>> Writes { get; } = [];

            public Exception? Failure { get; set; }

            public event Action<List<MonitorSettings>>? SettingsChanged { add { } remove { } }

            public List<MonitorSettings> LoadSettings() => [];

            public void SaveSettings(List<MonitorSettings> settings)
            {
                if (Failure is not null) throw Failure;
                Writes.Add(settings);
            }
        }

        private sealed class ErrorDialogSpy : IDialogService
        {
            public List<(string Text, string Title)> Errors { get; } = [];

            public void ShowError(string text, string title) => Errors.Add((text, title));

            public MessageBoxResult AskYesNoCancel(string question, string title) =>
                throw new InvalidOperationException("Saving never asks a question.");
        }
    }
}
