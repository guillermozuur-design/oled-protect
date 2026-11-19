using OLED_Sleeper.Storage;
using OLED_Sleeper.Tests.TestDoubles;

namespace OLED_Sleeper.Tests.Storage
{
    public class StorageRootTests
    {
        [Theory]
        [InlineData(false, @"C:\FakeAppData\OLED-Space-Sleeper")]
        [InlineData(true, @"C:\FakeApp\Data")]
        public void Folder_depends_on_distribution(bool portable, string expected)
        {
            var root = new StorageRoot(new FakeFileSystem(), portable);

            Assert.Equal(expected, root.DirectoryPath);
        }

        [Fact]
        public void Installed_build_follows_the_reported_appdata_folder()
        {
            var disk = new FakeFileSystem { AppDataRoot = @"D:\Roaming" };

            Assert.Equal(@"D:\Roaming\OLED-Space-Sleeper", new StorageRoot(disk, false).DirectoryPath);
        }

        [Fact]
        public void Portable_build_follows_the_exe_folder()
        {
            var disk = new FakeFileSystem { ExeFolder = @"E:\Tools\Sleeper" };

            Assert.Equal(@"E:\Tools\Sleeper\Data", new StorageRoot(disk, true).DirectoryPath);
        }
    }
}
