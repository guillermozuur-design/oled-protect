using OledProtect.Storage;
using OledProtect.Tests.TestDoubles;

namespace OledProtect.Tests.Storage
{
    public class StorageRootPreparerTests
    {
        private const string Installed = @"C:\FakeAppData\OledProtect";

        private readonly FakeFileSystem _disk = new();

        private bool Prepare(out string? message, bool portable = false) =>
            new StorageRootPreparer(_disk, new StorageRoot(_disk, portable)).TryPrepare(out message);

        [Fact]
        public void Healthy_folder_is_created_and_reports_no_error()
        {
            bool ok = Prepare(out var message);

            Assert.True(ok);
            Assert.Null(message);
            Assert.Equal([Installed], _disk.Mkdirs);
        }

        [Fact]
        public void Former_app_folder_is_taken_over_on_first_start()
        {
            _disk.Seed(@"C:\FakeAppData\OLED-Space-Sleeper\flights.json", "{ }");
            _disk.Seed(@"C:\FakeAppData\OLED-Space-Sleeper\Logs\log.txt", "x");

            Assert.True(Prepare(out _));

            Assert.Equal("{ }", _disk.Peek(Installed + @"\flights.json"));
            Assert.Equal("x", _disk.Peek(Installed + @"\Logs\log.txt"));
            Assert.Null(_disk.Peek(@"C:\FakeAppData\OLED-Space-Sleeper\flights.json"));
        }

        [Fact]
        public void Former_folder_is_left_alone_once_the_new_one_exists()
        {
            _disk.Seed(Installed + @"\flights.json", "new");
            _disk.Seed(@"C:\FakeAppData\OLED-Space-Sleeper\flights.json", "old");

            Assert.True(Prepare(out _));

            Assert.Equal("new", _disk.Peek(Installed + @"\flights.json"));
            Assert.Equal("old", _disk.Peek(@"C:\FakeAppData\OLED-Space-Sleeper\flights.json"));
        }

        [Fact]
        public void Probe_file_is_cleaned_up()
        {
            Prepare(out _);

            Assert.False(_disk.FileExists(Installed + @"\.write-probe"));
        }

        [Fact]
        public void Portable_build_prepares_the_data_folder_next_to_the_exe()
        {
            Assert.True(Prepare(out _, portable: true));
            Assert.Equal([@"C:\FakeApp\Data"], _disk.Mkdirs);
        }

        [Theory]
        [InlineData(FakeDiskFault.CreateDirectory)]
        [InlineData(FakeDiskFault.Write)]
        [InlineData(FakeDiskFault.Delete)]
        public void Any_io_failure_is_reported_with_the_folder_and_cause(FakeDiskFault fault)
        {
            _disk.Faults = fault;

            bool ok = Prepare(out var message);

            Assert.False(ok);
            Assert.NotNull(message);
            Assert.Contains(Installed, message);
            Assert.Contains("Simulated", message);
        }
    }
}
