using OledProtect.Storage;
using OledProtect.Tests.TestDoubles;

namespace OledProtect.Tests.Storage
{
    public class AppDataFileStoreTests
    {
        private const string Name = "state.json";
        private const string Main = @"C:\FakeAppData\OledProtect\state.json";
        private const string Bak = Main + ".bak";
        private const string Tmp = Main + ".tmp";

        private const string Good = """{ "A": 40 }""";
        private const string Broken = """{ "A": """;

        private readonly FakeFileSystem _disk = new();
        private readonly AppDataFileStore _sut;

        public AppDataFileStoreTests() => _sut = new AppDataFileStore(_disk, new StorageRoot(_disk, false));

        private uint? ReadA() => _sut.Read<Dictionary<string, uint>>(Name)?["A"];

        private bool WriteA(uint value) => _sut.TryWrite(Name, new Dictionary<string, uint> { ["A"] = value });

        // ---- reading ----

        [Theory]
        [InlineData(Good, null, 40u)]       // normal case
        [InlineData(null, Good, 40u)]       // main missing -> backup
        [InlineData(Broken, Good, 40u)]     // main truncated -> backup
        [InlineData(Broken, Broken, null)]  // nothing usable
        [InlineData(null, null, null)]      // first run
        [InlineData("null", Good, null)]    // a literal null is a valid read, not a reason to fall back
        public void Read_prefers_main_file_and_falls_back_to_backup(string? main, string? bak, uint? expected)
        {
            if (main is not null) _disk.Seed(Main, main);
            if (bak is not null) _disk.Seed(Bak, bak);

            Assert.Equal(expected, ReadA());
        }

        [Fact]
        public void Read_returns_default_when_the_disk_errors()
        {
            _disk.Seed(Main, Good);
            _disk.Seed(Bak, Good);
            _disk.Faults = FakeDiskFault.Read;

            Assert.Null(ReadA());
        }

        // ---- writing ----

        [Fact]
        public void First_write_creates_the_file_without_backup_or_leftovers()
        {
            Assert.True(WriteA(40));

            Assert.Contains("\"A\": 40", _disk.Peek(Main));
            Assert.False(_disk.FileExists(Bak));
            Assert.False(_disk.FileExists(Tmp));
        }

        [Fact]
        public void Overwrite_keeps_previous_version_as_backup()
        {
            _disk.Seed(Main, Good);

            Assert.True(WriteA(5));

            Assert.Contains("\"A\": 5", _disk.Peek(Main));
            Assert.Equal(Good, _disk.Peek(Bak));
            Assert.False(_disk.FileExists(Tmp));
        }

        [Fact]
        public void Output_is_indented_json()
        {
            WriteA(1);

            Assert.Contains(Environment.NewLine, _disk.Peek(Main));
        }

        [Theory]
        [InlineData(FakeDiskFault.Write)]
        [InlineData(FakeDiskFault.Replace)]
        public void Failed_write_reports_false_and_leaves_main_file_untouched(FakeDiskFault fault)
        {
            _disk.Seed(Main, Good);
            _disk.Faults = fault;

            Assert.False(WriteA(5));
            Assert.Equal(Good, _disk.Peek(Main));
        }

        [Fact]
        public void Written_value_reads_back_unchanged()
        {
            WriteA(40);
            WriteA(41);

            Assert.Equal(41u, ReadA());
        }
    }
}
