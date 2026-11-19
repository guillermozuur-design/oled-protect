using OledProtect.Storage;
using OledProtect.Tests.TestDoubles;

namespace OledProtect.Tests.Storage
{
    public class StorageRootTests
    {
        private static string RootFor(bool portable, string appData = @"C:\Users\x\AppData\Roaming", string exe = @"C:\Apps\Protect") =>
            new StorageRoot(new FakeFileSystem { AppDataRoot = appData, ExeFolder = exe }, portable).DirectoryPath;

        [Fact]
        public void Installed_copy_uses_its_own_folder_in_roaming_appdata()
        {
            Assert.Equal(@"C:\Users\x\AppData\Roaming\OledProtect", RootFor(portable: false));
        }

        [Fact]
        public void Portable_copy_keeps_its_data_beside_the_program()
        {
            Assert.Equal(@"C:\Apps\Protect\Data", RootFor(portable: true));
        }

        [Theory]
        [InlineData(@"D:\Profile\Roaming", @"D:\Profile\Roaming\OledProtect")]
        [InlineData(@"\\server\home\roam", @"\\server\home\roam\OledProtect")]
        public void Installed_path_follows_whatever_appdata_the_system_reports(string appData, string expected)
        {
            Assert.Equal(expected, RootFor(portable: false, appData: appData));
        }

        [Theory]
        [InlineData(@"E:\Stick\Tools", @"E:\Stick\Tools\Data")]
        [InlineData(@"C:\Program Files\OLED Protect", @"C:\Program Files\OLED Protect\Data")]
        public void Portable_path_follows_the_program_folder(string exe, string expected)
        {
            Assert.Equal(expected, RootFor(portable: true, exe: exe));
        }
    }
}
