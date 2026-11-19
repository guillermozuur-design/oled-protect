using OledProtect.Storage;
using OledProtect.Storage.Interfaces;
using OledProtect.Tests.TestDoubles;

namespace OledProtect.Tests.Storage
{
    public class StorageRootPreparerTests
    {
        private const string AppData = @"C:\FakeAppData";
        private const string Target = AppData + @"\OledProtect";
        private const string Legacy = AppData + @"\" + StorageRootPreparer.FormerFolderName;

        private static (bool Ok, string? Error) Run(FakeFileSystem disk, bool portable = false)
        {
            var preparer = new StorageRootPreparer(disk, new StorageRoot(disk, portable));
            bool ok = preparer.TryPrepare(out string? error);
            return (ok, error);
        }

        [Fact]
        public void A_writable_folder_passes_without_a_message()
        {
            var disk = new FakeFileSystem();

            var (ok, error) = Run(disk);

            Assert.True(ok);
            Assert.Null(error);
            Assert.Equal(new[] { Target }, disk.CreatedFolders);
        }

        [Fact]
        public void The_write_check_leaves_no_files_behind()
        {
            var disk = new FakeFileSystem();

            Run(disk);

            Assert.Empty(disk.AllFiles);
        }

        [Fact]
        public void Portable_mode_checks_the_data_folder_next_to_the_program()
        {
            var disk = new FakeFileSystem();

            Assert.True(Run(disk, portable: true).Ok);
            Assert.Equal(new[] { @"C:\FakeApp\Data" }, disk.CreatedFolders);
        }

        [Fact]
        public void The_folder_of_the_old_app_name_is_moved_into_place_once()
        {
            var disk = new FakeFileSystem()
                .With(Legacy + @"\settings.json", "S")
                .With(Legacy + @"\Logs\today.log", "L");

            Assert.True(Run(disk).Ok);

            Assert.Equal("S", disk.ContentOf(Target + @"\settings.json"));
            Assert.Equal("L", disk.ContentOf(Target + @"\Logs\today.log"));
            Assert.False(disk.DirectoryExists(Legacy));
        }

        [Fact]
        public void An_existing_new_folder_wins_over_the_old_one()
        {
            var disk = new FakeFileSystem()
                .With(Target + @"\settings.json", "current")
                .With(Legacy + @"\settings.json", "stale");

            Assert.True(Run(disk).Ok);

            Assert.Equal("current", disk.ContentOf(Target + @"\settings.json"));
            Assert.Equal("stale", disk.ContentOf(Legacy + @"\settings.json"));
        }

        [Fact]
        public void A_takeover_that_fails_still_starts_with_a_fresh_folder()
        {
            var disk = new FakeFileSystem()
                .With(Legacy + @"\settings.json", "S")
                .BreakOn(nameof(IFileSystem.MoveDirectory));

            var (ok, error) = Run(disk);

            Assert.True(ok, error);
            Assert.Contains(Target, disk.CreatedFolders);
            Assert.Equal("S", disk.ContentOf(Legacy + @"\settings.json"));
        }

        [Theory]
        [InlineData(nameof(IFileSystem.CreateDirectory))]
        [InlineData(nameof(IFileSystem.WriteAllText))]
        [InlineData(nameof(IFileSystem.DeleteFile))]
        public void A_refused_disk_call_fails_with_folder_and_reason(string brokenCall)
        {
            var disk = new FakeFileSystem().BreakOn(brokenCall);

            var (ok, error) = Run(disk);

            Assert.False(ok);
            Assert.NotNull(error);
            Assert.Contains(Target, error);
            Assert.Contains(FakeFileSystem.FailureText, error);
        }
    }
}
