using OLED_Sleeper.Storage.Interfaces;
using System.IO;

namespace OLED_Sleeper.Storage
{
    // Portable builds keep everything in a "Data" folder next to the exe; installed builds use a folder
    // under the roaming AppData. Both names decide where existing users' files are found.
    public sealed class StorageRoot(IFileSystem fileSystem, bool isPortable) : IStorageRoot
    {
        public string DirectoryPath { get; } = isPortable
            ? Path.Combine(fileSystem.GetApplicationDirectoryPath(), "Data")
            : Path.Combine(fileSystem.GetApplicationDataPath(), "OLED-Space-Sleeper");
    }
}
