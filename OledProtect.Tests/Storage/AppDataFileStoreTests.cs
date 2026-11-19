using OledProtect.Storage;
using OledProtect.Storage.Interfaces;
using OledProtect.Tests.TestDoubles;

namespace OledProtect.Tests.Storage
{
    public class AppDataFileStoreTests
    {
        // Payload shape used throughout; any JSON-friendly type would do.
        public sealed record Counter(string Label, int Count);

        private const string Folder = @"C:\FakeAppData\OledProtect";
        private const string FileName = "counter.json";
        private const string Live = Folder + @"\" + FileName;
        private const string Previous = Live + ".bak";
        private const string Staging = Live + ".tmp";

        private const string SevenJson = """{"Label":"seven","Count":7}""";
        private const string Truncated = """{"Label":"sev""";

        private static (FakeFileSystem Disk, AppDataFileStore Store) Fresh(bool portable = false)
        {
            var disk = new FakeFileSystem();
            return (disk, new AppDataFileStore(disk, new StorageRoot(disk, portable)));
        }

        public static TheoryData<string?, string?, int?> LoadCases => new()
        {
            { SevenJson, null, 7 },
            { SevenJson, """{"Label":"old","Count":3}""", 7 },
            { null, SevenJson, 7 },
            { Truncated, SevenJson, 7 },
            { "[1, 2]", SevenJson, 7 },
            { Truncated, Truncated, null },
            { null, null, null },
        };

        [Theory]
        [MemberData(nameof(LoadCases))]
        public void Loading_uses_the_live_file_and_otherwise_the_previous_copy(string? live, string? previous, int? expectedCount)
        {
            var (disk, store) = Fresh();
            if (live != null) disk.With(Live, live);
            if (previous != null) disk.With(Previous, previous);

            Counter? loaded = store.Read<Counter>(FileName);

            Assert.Equal(expectedCount, loaded?.Count);
        }

        [Fact]
        public void A_live_file_holding_json_null_counts_as_loaded()
        {
            var (disk, store) = Fresh();
            disk.With(Live, "null").With(Previous, SevenJson);

            Assert.Null(store.Read<Counter>(FileName));
        }

        [Fact]
        public void An_unreadable_disk_yields_the_default_instead_of_throwing()
        {
            var (disk, store) = Fresh();
            disk.With(Live, SevenJson).With(Previous, SevenJson).BreakOn(nameof(IFileSystem.ReadAllText));

            Assert.Null(store.Read<Counter>(FileName));
        }

        [Fact]
        public void Portable_builds_read_from_the_data_folder_beside_the_exe()
        {
            var (disk, store) = Fresh(portable: true);
            disk.With(@"C:\FakeApp\Data\" + FileName, SevenJson);

            Assert.Equal(new Counter("seven", 7), store.Read<Counter>(FileName));
        }

        [Fact]
        public void First_save_leaves_exactly_one_file_behind()
        {
            var (disk, store) = Fresh();

            bool saved = store.TryWrite(FileName, new Counter("a", 1));

            Assert.True(saved);
            Assert.Equal([Live], disk.AllFiles);
        }

        [Fact]
        public void Saving_over_an_existing_file_moves_the_old_text_to_bak()
        {
            var (disk, store) = Fresh();
            disk.With(Live, SevenJson);

            Assert.True(store.TryWrite(FileName, new Counter("eight", 8)));

            Assert.Equal(SevenJson, disk.ContentOf(Previous));
            Assert.Equal(new Counter("eight", 8), store.Read<Counter>(FileName));
            Assert.DoesNotContain(Staging, disk.AllFiles);
        }

        [Fact]
        public void Saved_json_is_spread_over_several_lines()
        {
            var (disk, store) = Fresh();

            store.TryWrite(FileName, new Counter("x", 2));

            string text = disk.ContentOf(Live)!;
            Assert.True(text.Split('\n').Length > 2, text);
            Assert.Contains("\"Count\": 2", text);
        }

        [Fact]
        public void Repeated_saves_end_with_the_newest_value()
        {
            var (_, store) = Fresh();

            for (int n = 1; n <= 3; n++)
                store.TryWrite(FileName, new Counter("loop", n));

            Assert.Equal(3, store.Read<Counter>(FileName)!.Count);
        }

        [Theory]
        [InlineData(nameof(IFileSystem.WriteAllText))]
        [InlineData(nameof(IFileSystem.Replace))]
        public void A_failing_save_returns_false_and_keeps_the_live_file(string brokenCall)
        {
            var (disk, store) = Fresh();
            disk.With(Live, SevenJson).BreakOn(brokenCall);

            Assert.False(store.TryWrite(FileName, new Counter("lost", 0)));
            Assert.Equal(SevenJson, disk.ContentOf(Live));
        }

        [Fact]
        public void A_failing_first_rename_returns_false_without_creating_the_file()
        {
            var (disk, store) = Fresh();
            disk.BreakOn(nameof(IFileSystem.Move));

            Assert.False(store.TryWrite(FileName, new Counter("lost", 0)));
            Assert.Null(disk.ContentOf(Live));
        }

        private sealed class Loop
        {
            public Loop? Self { get; set; }
        }

        [Fact]
        public void A_value_that_cannot_be_serialized_is_reported_not_thrown()
        {
            var (disk, store) = Fresh();
            var loop = new Loop();
            loop.Self = loop;

            Assert.False(store.TryWrite(FileName, loop));
            Assert.Empty(disk.AllFiles);
        }
    }
}
