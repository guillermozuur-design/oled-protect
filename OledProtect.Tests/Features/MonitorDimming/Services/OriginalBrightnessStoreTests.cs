using OledProtect.Features.MonitorDimming.Services;
using OledProtect.Features.MonitorDimming.Services.Interfaces;

namespace OledProtect.Tests.Features.MonitorDimming.Services
{
    public sealed class OriginalBrightnessStoreTests
    {
        private const string Panel = "GSM-1";

        private readonly StateFile _disk = new();

        [Fact]
        public void Levels_left_by_the_previous_run_are_known_at_once()
        {
            _disk.OnDisk = new() { [Panel] = 80 };

            var store = new OriginalBrightnessStore(_disk);

            Assert.Equal(80u, LevelOf(store));
        }

        [Fact]
        public void Unknown_monitor_has_no_level()
        {
            Assert.False(new OriginalBrightnessStore(_disk).TryGetOriginal(Panel, out _));
        }

        [Fact]
        public void First_record_is_kept_and_written_through()
        {
            var store = new OriginalBrightnessStore(_disk);

            store.RecordOriginal(Panel, 80);

            Assert.Equal(80u, LevelOf(store));
            Assert.Equal([$"{Panel}=80"], _disk.Saves);
        }

        [Fact]
        public void Second_record_does_not_overwrite_the_first_and_saves_nothing()
        {
            var store = new OriginalBrightnessStore(_disk);
            store.RecordOriginal(Panel, 80);

            store.RecordOriginal(Panel, 5);

            Assert.Equal(80u, LevelOf(store));
            Assert.Single(_disk.Saves);
        }

        [Fact]
        public void Replace_overwrites_and_saves()
        {
            var store = new OriginalBrightnessStore(_disk);
            store.RecordOriginal(Panel, 80);

            store.ReplaceOriginal(Panel, 40);

            Assert.Equal(40u, LevelOf(store));
            Assert.Equal($"{Panel}=40", _disk.Saves[^1]);
        }

        [Fact]
        public void Replace_also_works_for_a_monitor_without_a_level()
        {
            var store = new OriginalBrightnessStore(_disk);

            store.ReplaceOriginal(Panel, 40);

            Assert.Equal(40u, LevelOf(store));
        }

        [Fact]
        public void Remove_forgets_the_level_and_saves_the_emptied_map()
        {
            var store = new OriginalBrightnessStore(_disk);
            store.RecordOriginal(Panel, 80);

            store.RemoveOriginal(Panel);

            Assert.False(store.TryGetOriginal(Panel, out _));
            Assert.Equal("", _disk.Saves[^1]);
        }

        [Fact]
        public void Removing_an_unknown_monitor_leaves_the_file_alone()
        {
            new OriginalBrightnessStore(_disk).RemoveOriginal(Panel);

            Assert.Empty(_disk.Saves);
        }

        [Fact]
        public void Snapshot_is_a_copy()
        {
            var store = new OriginalBrightnessStore(_disk);
            store.RecordOriginal(Panel, 80);

            store.GetAll().Remove(Panel);

            Assert.Equal(80u, LevelOf(store));
        }

        [Fact]
        public void Saved_maps_are_copies_that_later_changes_do_not_touch()
        {
            var store = new OriginalBrightnessStore(_disk);
            store.RecordOriginal(Panel, 80);
            var firstSave = _disk.LastSaved;

            store.ReplaceOriginal(Panel, 40);

            Assert.Equal(80u, firstSave![Panel]);
        }

        private static uint LevelOf(OriginalBrightnessStore store)
        {
            Assert.True(store.TryGetOriginal(Panel, out uint level));
            return level;
        }

        // Each save is written down as "id=level" pairs, joined by commas in key order.
        private sealed class StateFile : IMonitorBrightnessStateService
        {
            public Dictionary<string, uint> OnDisk { get; set; } = [];

            public List<string> Saves { get; } = [];

            public Dictionary<string, uint>? LastSaved { get; private set; }

            public Dictionary<string, uint> LoadState() => OnDisk;

            public void SaveState(Dictionary<string, uint> snapshot)
            {
                LastSaved = snapshot;
                Saves.Add(string.Join(",", snapshot.OrderBy(p => p.Key).Select(p => $"{p.Key}={p.Value}")));
            }
        }
    }
}
