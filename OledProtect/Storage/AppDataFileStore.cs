using OledProtect.Storage.Interfaces;
using Serilog;
using System.IO;
using System.Text.Json;

namespace OledProtect.Storage
{
    // Small JSON files in the storage root, saved so that a crash or power cut mid-save never leaves the
    // user empty-handed: the new text is staged beside the file and swapped in, and the version it
    // replaces is kept as "<name>.bak", which loading falls back on.
    public sealed class AppDataFileStore(IFileSystem fileSystem, IStorageRoot storageRoot) : IAppDataFileStore
    {
        private const string StagedSuffix = ".tmp";
        private const string PreviousSuffix = ".bak";

        private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };

        public T? Read<T>(string fileName)
        {
            string current = Locate(fileName);
            string[] candidates = [current, current + PreviousSuffix];

            // A file containing just `null` is a successful load; only absent or broken files move on.
            foreach (string path in candidates.Where(fileSystem.FileExists))
            {
                try
                {
                    var value = JsonSerializer.Deserialize<T>(fileSystem.ReadAllText(path));
                    if (path != current)
                        Log.Warning("{FileName} could not be used; loaded the previous version from {Path}.", fileName, path);
                    return value;
                }
                catch (Exception problem)
                {
                    Log.Error(problem, "Skipping unreadable data file {Path}.", path);
                }
            }

            return default;
        }

        public bool TryWrite<T>(string fileName, T value)
        {
            string current = Locate(fileName);
            string staged = current + StagedSuffix;

            try
            {
                // File.Replace only works on an existing target, so the very first save is a plain rename.
                bool firstSave = !fileSystem.FileExists(current);
                fileSystem.WriteAllText(staged, JsonSerializer.Serialize(value, Pretty));

                if (firstSave)
                    fileSystem.Move(staged, current);
                else
                    fileSystem.Replace(staged, current, current + PreviousSuffix);
            }
            catch (Exception problem)
            {
                Log.Error(problem, "Saving {Path} failed; whatever was on disk stays as it was.", current);
                return false;
            }

            return true;
        }

        private string Locate(string fileName) => Path.Combine(storageRoot.DirectoryPath, fileName);
    }
}
