using OledProtect.Storage.Interfaces;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace OledProtect.Storage
{
    // Production IFileSystem: straight pass-through to the BCL, nothing worth unit testing.
    [ExcludeFromCodeCoverage]
    public sealed class FileSystem : IFileSystem
    {
        public string GetApplicationDataPath() =>
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        public string GetApplicationDirectoryPath() =>
            Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);

        public void CreateDirectory(string path) =>
            _ = Directory.CreateDirectory(path);

        public bool FileExists(string path) =>
            File.Exists(path);

        public string ReadAllText(string path) =>
            File.ReadAllText(path);

        public void WriteAllText(string path, string contents) =>
            File.WriteAllText(path, contents);

        // Metadata errors (e.g. ACLs on some network shares) should not block saving the data itself.
        public void Replace(string sourcePath, string destinationPath, string backupPath) =>
            File.Replace(sourcePath, destinationPath, backupPath, true);

        public void Move(string sourcePath, string destinationPath) =>
            File.Move(sourcePath, destinationPath, overwrite: false);

        public void DeleteFile(string path) =>
            File.Delete(path);

        public bool DirectoryExists(string path) =>
            Directory.Exists(path);

        public void MoveDirectory(string sourcePath, string destinationPath) =>
            Directory.Move(sourcePath, destinationPath);
    }
}
