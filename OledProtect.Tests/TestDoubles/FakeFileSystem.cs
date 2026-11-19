using OledProtect.Storage.Interfaces;
using System.IO;

namespace OledProtect.Tests.TestDoubles
{
    // A RAM-only drive for storage tests. Files are a path -> text map (case-insensitive like NTFS); a
    // folder exists when it was created or something lies beneath it. Rename-style calls refuse the same
    // situations Win32 refuses, so picking the wrong call is caught. Any call can be made to fail through
    // BreakOn, using the IFileSystem member name, e.g. disk.BreakOn(nameof(IFileSystem.Replace)).
    public sealed class FakeFileSystem : IFileSystem
    {
        private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;

        private readonly Dictionary<string, string> _texts = new(PathComparer);
        private readonly HashSet<string> _folders = new(PathComparer);
        private readonly HashSet<string> _broken = new(StringComparer.Ordinal);

        public string AppDataRoot { get; init; } = @"C:\FakeAppData";

        public string ExeFolder { get; init; } = @"C:\FakeApp";

        // Arguments of every CreateDirectory call that went through, oldest first.
        public List<string> CreatedFolders { get; } = [];

        // Text used in the IOException of a broken call; tests look for it in error messages.
        public const string FailureText = "fake disk refused";

        public FakeFileSystem BreakOn(params string[] operations)
        {
            _broken.UnionWith(operations);
            return this;
        }

        // Test-side access that never fails.
        public FakeFileSystem With(string path, string text)
        {
            _texts[path] = text;
            return this;
        }

        public string? ContentOf(string path) => _texts.TryGetValue(path, out var text) ? text : null;

        public IReadOnlyCollection<string> AllFiles => _texts.Keys;

        public string GetApplicationDataPath() => AppDataRoot;

        public string GetApplicationDirectoryPath() => ExeFolder;

        public void CreateDirectory(string path)
        {
            Guard(nameof(CreateDirectory), path);
            _folders.Add(path);
            CreatedFolders.Add(path);
        }

        public bool FileExists(string path) => _texts.ContainsKey(path);

        public string ReadAllText(string path)
        {
            Guard(nameof(ReadAllText), path);
            return Require(path);
        }

        public void WriteAllText(string path, string contents)
        {
            Guard(nameof(WriteAllText), path);
            _texts[path] = contents;
        }

        public void Replace(string sourcePath, string destinationPath, string backupPath)
        {
            Guard(nameof(Replace), destinationPath);
            (string incoming, string outgoing) = (Require(sourcePath), Require(destinationPath));
            _texts.Remove(sourcePath);
            _texts[destinationPath] = incoming;
            _texts[backupPath] = outgoing;
        }

        public void Move(string sourcePath, string destinationPath)
        {
            Guard(nameof(Move), destinationPath);
            if (FileExists(destinationPath))
                throw new IOException($"{destinationPath} is already there");
            _texts[destinationPath] = Require(sourcePath);
            _texts.Remove(sourcePath);
        }

        public void DeleteFile(string path)
        {
            Guard(nameof(DeleteFile), path);
            _texts.Remove(path);
        }

        public bool DirectoryExists(string path) =>
            _folders.Contains(path) || _texts.Keys.Any(file => IsBelow(file, path));

        public void MoveDirectory(string sourcePath, string destinationPath)
        {
            Guard(nameof(MoveDirectory), destinationPath);
            if (DirectoryExists(destinationPath))
                throw new IOException($"{destinationPath} is already there");

            foreach (var file in _texts.Keys.Where(f => IsBelow(f, sourcePath)).ToArray())
            {
                string relative = Path.GetRelativePath(sourcePath, file);
                _texts[Path.Combine(destinationPath, relative)] = _texts[file];
                _texts.Remove(file);
            }

            if (_folders.Remove(sourcePath))
                _folders.Add(destinationPath);
        }

        private static bool IsBelow(string file, string folder) =>
            file.StartsWith(Path.TrimEndingDirectorySeparator(folder) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

        private string Require(string path) =>
            ContentOf(path) ?? throw new FileNotFoundException("no such fake file", path);

        private void Guard(string operation, string path)
        {
            if (_broken.Contains(operation))
                throw new IOException($"{FailureText}: {operation} {path}");
        }
    }
}
