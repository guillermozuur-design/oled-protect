using OledProtect.Features.UserSettings.Models;
using OledProtect.Features.UserSettings.Services;
using OledProtect.Storage.Interfaces;

namespace OledProtect.Tests.Features.UserSettings.Services
{
    public class MonitorSettingsFileServiceTests
    {
        private const int CurrentSchema = 1;

        // Keeps the last saved object per file name; can refuse saves or blow up on reads.
        private sealed class MemoryStore : IAppDataFileStore
        {
            public Dictionary<string, object?> Files { get; } = [];
            public List<(string Name, object? Value)> Saves { get; } = [];
            public bool RefuseSaves { get; set; }
            public Exception? ReadFailure { get; set; }

            public T? Read<T>(string fileName)
            {
                if (ReadFailure != null)
                    throw ReadFailure;
                return Files.TryGetValue(fileName, out var stored) ? (T?)stored : default;
            }

            public bool TryWrite<T>(string fileName, T value)
            {
                Saves.Add((fileName, value));
                if (RefuseSaves)
                    return false;
                Files[fileName] = value;
                return true;
            }
        }

        private readonly MemoryStore _store = new();

        private MonitorSettingsFileService NewService() => new(_store);

        private static MonitorSettings Screen(string id, double dimLevel = 15) => new() { HardwareId = id, DimLevel = dimLevel };

        private void Stored(int schema, List<MonitorSettings> monitors) =>
            _store.Files["settings.json"] = new MonitorSettingsDocument { SchemaVersion = schema, Monitors = monitors };

        private MonitorSettingsDocument OnlySavedDocument()
        {
            var (name, value) = Assert.Single(_store.Saves);
            Assert.Equal("settings.json", name);
            return Assert.IsType<MonitorSettingsDocument>(value);
        }

        [Fact]
        public void Loading_returns_what_settings_json_holds()
        {
            Stored(CurrentSchema, [Screen("DEL01"), Screen("LG02")]);

            var loaded = NewService().LoadSettings();

            Assert.Equal(new[] { "DEL01", "LG02" }, loaded.Select(m => m.HardwareId));
        }

        [Fact]
        public void Loading_with_nothing_stored_gives_no_monitors()
        {
            Assert.Empty(NewService().LoadSettings());
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-7)]
        [InlineData(CurrentSchema + 1)]
        public void Loading_ignores_a_file_with_a_foreign_schema_number(int schema)
        {
            Stored(schema, [Screen("DEL01")]);

            Assert.Empty(NewService().LoadSettings());
        }

        [Fact]
        public void Loading_copes_with_a_monitors_list_set_to_null()
        {
            Stored(CurrentSchema, null!);

            Assert.Empty(NewService().LoadSettings());
        }

        [Fact]
        public void Saving_on_a_clean_install_stores_the_given_screens_with_the_schema_number()
        {
            NewService().SaveSettings([Screen("DEL01")]);

            var saved = OnlySavedDocument();
            Assert.Equal(CurrentSchema, saved.SchemaVersion);
            Assert.Equal("DEL01", Assert.Single(saved.Monitors).HardwareId);
        }

        [Fact]
        public void Saving_keeps_stored_screens_that_are_not_connected_now()
        {
            Stored(CurrentSchema, [Screen("DEL01"), Screen("unplugged")]);

            NewService().SaveSettings([Screen("DEL01")]);

            Assert.Equal(new[] { "DEL01", "unplugged" }, OnlySavedDocument().Monitors.Select(m => m.HardwareId));
        }

        [Fact]
        public void Saving_overrides_the_stored_entry_of_a_screen_that_is_passed_in()
        {
            Stored(CurrentSchema, [Screen("DEL01", dimLevel: 15)]);

            NewService().SaveSettings([Screen("DEL01", dimLevel: 64)]);

            Assert.Equal(64, Assert.Single(OnlySavedDocument().Monitors).DimLevel);
        }

        [Fact]
        public void Saving_drops_stored_screens_from_an_outdated_schema()
        {
            Stored(CurrentSchema - 1, [Screen("ancient")]);

            NewService().SaveSettings([Screen("DEL01")]);

            Assert.Equal("DEL01", Assert.Single(OnlySavedDocument().Monitors).HardwareId);
        }

        [Fact]
        public void Listeners_hear_about_a_save_with_exactly_the_list_that_was_passed()
        {
            Stored(CurrentSchema, [Screen("DEL01"), Screen("other")]);
            var service = NewService();
            var notifications = new List<List<MonitorSettings>>();
            service.SettingsChanged += notifications.Add;
            List<MonitorSettings> edited = [Screen("DEL01")];

            service.SaveSettings(edited);

            Assert.Same(edited, Assert.Single(notifications));
        }

        [Fact]
        public void Listeners_hear_nothing_when_the_store_refuses_the_save()
        {
            _store.RefuseSaves = true;
            var service = NewService();
            bool notified = false;
            service.SettingsChanged += _ => notified = true;

            service.SaveSettings([Screen("DEL01")]);

            Assert.False(notified);
        }

        [Fact]
        public void A_listener_that_throws_does_not_escape_SaveSettings()
        {
            var service = NewService();
            service.SettingsChanged += _ => throw new InvalidOperationException("listener broke");

            var thrown = Record.Exception(() => service.SaveSettings([Screen("DEL01")]));

            Assert.Null(thrown);
        }

        [Fact]
        public void A_store_that_throws_does_not_escape_SaveSettings()
        {
            _store.ReadFailure = new System.IO.IOException("drive vanished");

            var thrown = Record.Exception(() => NewService().SaveSettings([Screen("DEL01")]));

            Assert.Null(thrown);
            Assert.Empty(_store.Saves);
        }
    }
}
