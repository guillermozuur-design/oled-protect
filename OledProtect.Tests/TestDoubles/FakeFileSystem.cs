using OledProtect.Storage.Interfaces;
using System.IO;

namespace OledProtect.Tests.TestDoubles
{
    // Which operations of FakeFileSystem should throw an IOException.
    [Flags]
    public enum FakeDiskFault
    {
        None = 0,
        Read = 1,
        Write = 2,
        Replace = 4,
        Delete = 8,
        CreateDirectory = 16,
    }

    // In-memory disk. Replace/Move enforce the same preconditions as the real Win32 calls, so code that
    // picks the wrong one fails in tests too. Faults simulates I/O errors per operation.
    public class FakeFileSystem : IFileSystem
    {
        private readonly Dictionary<string, string> _disk = new(StringComparer.OrdinalIgnoreCase);

        public string AppDataRoot { get; init; } = @"C:\FakeAppData";

        public string ExeFolder { get; init; } = @"C:\FakeApp";

        public FakeDiskFault Faults { get; set; }

        // Every folder creation request, in call order.
        public List<string> Mkdirs { get; } = [];

        // Setup/inspection helpers; they bypass Faults on purpose.
        public void Seed(string path, string contents) => _disk[path] = contents;

        public string? Peek(string path) => _disk.GetValueOrDefault(path);

        string IFileSystem.GetApplicationDataPath() => AppDataRoot;

        string IFileSystem.GetApplicationDirectoryPath() => ExeFolder;

        void IFileSystem.CreateDirectory(string path)
        {
            Trip(FakeDiskFault.CreateDirectory, path);
            Mkdirs.Add(path);
        }

        public bool FileExists(string path) => _disk.ContainsKey(path);

        string IFileSystem.ReadAllText(string path)
        {
            Trip(FakeDiskFault.Read, path);
            return Existing(path);
        }

        void IFileSystem.WriteAllText(string path, string contents)
        {
            Trip(FakeDiskFault.Write, path);
            _disk[path] = contents;
        }

        void IFileSystem.Replace(string sourcePath, string destinationPath, string backupPath)
        {
            Trip(FakeDiskFault.Replace, destinationPath);
            string incoming = Existing(sourcePath);
            string previous = Existing(destinationPath);

            _disk.Remove(sourcePath);
            _disk[backupPath] = previous;
            _disk[destinationPath] = incoming;
        }

        void IFileSystem.Move(string sourcePath, string destinationPath)
        {
            if (_disk.ContainsKey(destinationPath))
                throw new IOException("Target already exists: " + destinationPath);

            _disk[destinationPath] = Existing(sourcePath);
            _disk.Remove(sourcePath);
        }

        void IFileSystem.DeleteFile(string path)
        {
            Trip(FakeDiskFault.Delete, path);
            _disk.Remove(path);
        }

        // Folders exist when any file lives below them, or when they were created.
        public bool DirectoryExists(string path)
        {
            string prefix = path.TrimEnd('\\') + "\\";
            return Mkdirs.Contains(path, StringComparer.OrdinalIgnoreCase)
                   || _disk.Keys.Any(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }

        void IFileSystem.MoveDirectory(string sourcePath, string destinationPath)
        {
            if (DirectoryExists(destinationPath))
                throw new IOException("Target already exists: " + destinationPath);
            string from = sourcePath.TrimEnd('\\') + "\\", to = destinationPath.TrimEnd('\\') + "\\";
            foreach (var key in _disk.Keys.Where(k => k.StartsWith(from, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                _disk[to + key[from.Length..]] = _disk[key];
                _disk.Remove(key);
            }
        }

        private string Existing(string path) =>
            _disk.TryGetValue(path, out var text) ? text : throw new FileNotFoundException(null, path);

        private void Trip(FakeDiskFault operation, string path)
        {
            if (Faults.HasFlag(operation))
                throw new IOException($"Simulated {operation} failure on {path}");
        }
    }
}
