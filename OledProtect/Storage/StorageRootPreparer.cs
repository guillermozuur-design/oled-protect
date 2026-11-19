using OledProtect.Storage.Interfaces;
using System.IO;

namespace OledProtect.Storage
{
    // Creating a folder that already exists says nothing about write access, so we also write and
    // remove a tiny marker file. Any failure along the way becomes a message for the user.
    public sealed class StorageRootPreparer(IFileSystem fileSystem, IStorageRoot storageRoot) : IStorageRootPreparer
    {
        private const string MarkerName = ".write-probe";

        // Until version 3 the app was called "OLED Space Sleeper" and kept its files in this folder next to
        // the new one; the first start under the new name takes it over (settings, logs, map tiles).
        public const string FormerFolderName = "OLED-Space-Sleeper";

        public bool TryPrepare(out string? error)
        {
            string folder = storageRoot.DirectoryPath;
            error = null;

            try
            {
                TakeOverFormerFolder(folder);
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

        private void TakeOverFormerFolder(string folder)
        {
            if (fileSystem.DirectoryExists(folder) || Path.GetDirectoryName(folder) is not { } parent)
                return;
            string former = Path.Combine(parent, FormerFolderName);
            if (!fileSystem.DirectoryExists(former))
                return;
            try
            {
                fileSystem.MoveDirectory(former, folder);
            }
            catch (IOException)
            {
                // In use or on another drive: start fresh rather than not at all.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static string DescribeFailure(string folder, Exception failure)
        {
            string nl = Environment.NewLine;
            return "OLED Protect kan niet schrijven naar:" + nl + folder + nl + nl + failure.Message;
        }
    }
}
