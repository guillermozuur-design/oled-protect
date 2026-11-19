using Moq;
using OledProtect.Features.UserSettings.Models;
using OledProtect.Features.UserSettings.Services;
using OledProtect.Storage.Interfaces;

namespace OledProtect.Tests.Features.UserSettings.Services
{
    public class MonitorSettingsFileServiceTests
    {
        private const string File = "settings.json";
        private const int Schema = 1;

        private readonly Mock<IAppDataFileStore> _store = new();
        private readonly MonitorSettingsFileService _sut;
        private readonly List<MonitorSettingsDocument> _writes = [];

        public MonitorSettingsFileServiceTests()
        {
            _store.Setup(s => s.TryWrite(File, It.IsAny<MonitorSettingsDocument>()))
                  .Callback<string, MonitorSettingsDocument>((_, doc) => _writes.Add(doc))
                  .Returns(true);
            _sut = new MonitorSettingsFileService(_store.Object);
        }

        private static MonitorSettings Monitor(string id, double dim = 15) => new() { HardwareId = id, DimLevel = dim };

        private void OnDisk(int version, params MonitorSettings[] monitors) =>
            _store.Setup(s => s.Read<MonitorSettingsDocument>(File))
                  .Returns(new MonitorSettingsDocument { SchemaVersion = version, Monitors = [.. monitors] });

        private MonitorSettingsDocument SingleWrite() => Assert.Single(_writes);

        // ---- loading ----

        [Fact]
        public void Load_returns_stored_monitors_for_the_current_schema()
        {
            OnDisk(Schema, Monitor("A"));

            Assert.Equal("A", Assert.Single(_sut.LoadSettings()).HardwareId);
        }

        [Fact]
        public void Load_without_a_file_gives_an_empty_list()
        {
            Assert.Empty(_sut.LoadSettings());
        }

        [Theory]
        [InlineData(0)]          // file from before versioning (key absent)
        [InlineData(-1)]         // garbage
        [InlineData(Schema + 1)] // written by a newer build
        public void Load_discards_other_schema_versions(int version)
        {
            OnDisk(version, Monitor("A"));

            Assert.Empty(_sut.LoadSettings());
        }

        [Fact]
        public void Load_survives_a_null_monitor_list()
        {
            _store.Setup(s => s.Read<MonitorSettingsDocument>(File))
                  .Returns(new MonitorSettingsDocument { SchemaVersion = Schema, Monitors = null! });

            Assert.Empty(_sut.LoadSettings());
        }

        // ---- saving ----

        [Fact]
        public void Save_on_a_fresh_install_writes_just_the_given_monitors_with_the_schema()
        {
            _sut.SaveSettings([Monitor("A")]);

            var doc = SingleWrite();
            Assert.Equal(Schema, doc.SchemaVersion);
            Assert.Equal("A", Assert.Single(doc.Monitors).HardwareId);
        }

        [Fact]
        public void Save_keeps_entries_of_monitors_that_were_not_passed_in()
        {
            OnDisk(Schema, Monitor("A"), Monitor("B"));

            _sut.SaveSettings([Monitor("A")]);

            Assert.Equal(["A", "B"], SingleWrite().Monitors.Select(m => m.HardwareId));
        }

        [Fact]
        public void Save_replaces_the_stored_entry_of_a_passed_monitor()
        {
            OnDisk(Schema, Monitor("A", dim: 15));

            _sut.SaveSettings([Monitor("A", dim: 50)]);

            Assert.Equal(50, Assert.Single(SingleWrite().Monitors).DimLevel);
        }

        [Fact]
        public void Save_ignores_stored_entries_from_another_schema()
        {
            OnDisk(Schema - 1, Monitor("Old"));

            _sut.SaveSettings([Monitor("A")]);

            Assert.Equal("A", Assert.Single(SingleWrite().Monitors).HardwareId);
        }

        [Fact]
        public void Successful_save_raises_SettingsChanged_with_the_callers_list()
        {
            OnDisk(Schema, Monitor("A"), Monitor("B"));
            List<MonitorSettings> given = [Monitor("A")];
            List<MonitorSettings>? received = null;
            _sut.SettingsChanged += list => received = list;

            _sut.SaveSettings(given);

            Assert.Same(given, received);
        }

        [Fact]
        public void Failed_write_does_not_raise_SettingsChanged()
        {
            _store.Setup(s => s.TryWrite(File, It.IsAny<MonitorSettingsDocument>())).Returns(false);
            int raised = 0;
            _sut.SettingsChanged += _ => raised++;

            _sut.SaveSettings([Monitor("A")]);

            Assert.Equal(0, raised);
        }

        [Fact]
        public void Throwing_subscriber_is_contained()
        {
            _sut.SettingsChanged += _ => throw new InvalidOperationException("boom");

            Assert.Null(Record.Exception(() => _sut.SaveSettings([Monitor("A")])));
        }

        [Fact]
        public void Throwing_store_is_contained()
        {
            _store.Setup(s => s.Read<MonitorSettingsDocument>(File)).Throws(new System.IO.IOException("disk gone"));

            Assert.Null(Record.Exception(() => _sut.SaveSettings([Monitor("A")])));
        }
    }
}
