namespace OledProtect.Storage.Interfaces
{
    // Thin seam over System.IO so storage code can run against an in-memory fake in tests.
    public interface IFileSystem
    {
        // %APPDATA% (roaming) of the signed-in user.
        string GetApplicationDataPath();

        // Folder holding the executable, no trailing slash.
        string GetApplicationDirectoryPath();

        // Idempotent: an existing folder is fine.
        void CreateDirectory(string path);

        bool FileExists(string path);

        string ReadAllText(string path);

        // Creates or overwrites.
        void WriteAllText(string path, string contents);

        // Atomic swap: sourcePath becomes destinationPath, the old destination lands in backupPath.
        // Win32 requires destinationPath to exist already; use Move for a first write.
        void Replace(string sourcePath, string destinationPath, string backupPath);

        // Fails when destinationPath already exists.
        void Move(string sourcePath, string destinationPath);

        // A missing file is not an error.
        void DeleteFile(string path);

        bool DirectoryExists(string path);

        // Renames a folder (same drive); fails when destinationPath already exists.
        void MoveDirectory(string sourcePath, string destinationPath);
    }
}
