using OledProtect.Storage.Interfaces;
using Serilog;
using System.IO;
using System.Text.Json;

namespace OledProtect.Storage
{
    // Crash-safe JSON files: every save goes to "<name>.tmp" first and is then swapped in, with the
    // previous version parked in "<name>.bak". Loading falls back to that .bak when the main file is
    // missing or damaged, so a power cut halfway through a save never costs the user their settings.
    public sealed class AppDataFileStore(IFileSystem fileSystem, IStorageRoot storageRoot) : IAppDataFileStore
    {
        private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

        private readonly string _folder = storageRoot.DirectoryPath;

        public T? Read<T>(string fileName)
        {
            string primary = PathOf(fileName);

            if (TryLoad(primary, out T? loaded))
                return loaded;

            if (TryLoad(BackupOf(primary), out T? recovered))
            {
                Log.Warning("{FileName} was unusable; using its .bak copy instead.", fileName);
                return recovered;
            }

            return default;
        }

        public bool TryWrite<T>(string fileName, T value)
        {
            string primary = PathOf(fileName);
            string staging = primary + ".tmp";

            try
            {
                fileSystem.WriteAllText(staging, JsonSerializer.Serialize(value, Json));
                SwapIn(staging, primary);
                return true;
            }
            catch (Exception failure)
            {
                Log.Error(failure, "Could not save {Path}; the previous version is kept.", primary);
                return false;
            }
        }

        // Replace needs an existing target; the very first save has none and simply renames.
        private void SwapIn(string staging, string primary)
        {
            if (fileSystem.FileExists(primary))
                fileSystem.Replace(staging, primary, BackupOf(primary));
            else
                fileSystem.Move(staging, primary);
        }

        // A file holding the JSON literal null counts as read (result null), so it does not
        // trigger the backup fallback.
        private bool TryLoad<T>(string path, out T? result)
        {
            result = default;
            if (!fileSystem.FileExists(path))
                return false;

            try
            {
                result = JsonSerializer.Deserialize<T>(fileSystem.ReadAllText(path));
                return true;
            }
            catch (Exception failure)
            {
                Log.Error(failure, "Could not load {Path}.", path);
                return false;
            }
        }

        private string PathOf(string fileName) => Path.Combine(_folder, fileName);

        private static string BackupOf(string path) => path + ".bak";
    }
}
