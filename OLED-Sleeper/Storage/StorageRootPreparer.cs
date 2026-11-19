using OLED_Sleeper.Storage.Interfaces;
using System.IO;

namespace OLED_Sleeper.Storage
{
    // Creating a folder that already exists says nothing about write access, so we also write and
    // remove a tiny marker file. Any failure along the way becomes a message for the user.
    public sealed class StorageRootPreparer(IFileSystem fileSystem, IStorageRoot storageRoot) : IStorageRootPreparer
    {
        private const string MarkerName = ".write-probe";

        public bool TryPrepare(out string? error)
        {
            string folder = storageRoot.DirectoryPath;
            error = null;

            try
            {
                fileSystem.CreateDirectory(folder);
                string marker = Path.Combine(folder, MarkerName);
                fileSystem.WriteAllText(marker, "");
                fileSystem.DeleteFile(marker);
            }
            catch (Exception failure)
            {
                error = DescribeFailure(folder, failure);
            }

            return error is null;
        }

        private static string DescribeFailure(string folder, Exception failure)
        {
            string nl = Environment.NewLine;
            return "OLED Space Sleeper kan niet schrijven naar:" + nl + folder + nl + nl + failure.Message;
        }
    }
}
