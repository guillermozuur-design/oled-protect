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

        // Files that show a folder really holds this app's settings (and not, say, an unpacked program).
        private static readonly string[] SettingsFiles = { "settings.json", "general.json", "flights.json" };

        private void TakeOverFormerFolder(string folder)
        {
            // Only the installed build: its folder sits directly in AppData. A portable build keeps its
            // data next to the exe, where a folder with the former name may well be an old unpacked copy.
            if (fileSystem.DirectoryExists(folder) || Path.GetDirectoryName(folder) is not { } parent
                || !string.Equals(Path.TrimEndingDirectorySeparator(parent),
                                  Path.TrimEndingDirectorySeparator(fileSystem.GetApplicationDataPath()),
                                  StringComparison.OrdinalIgnoreCase))
                return;
            string former = Path.Combine(parent, FormerFolderName);
            if (!fileSystem.DirectoryExists(former) || !SettingsFiles.Any(f => fileSystem.FileExists(Path.Combine(former, f))))
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
