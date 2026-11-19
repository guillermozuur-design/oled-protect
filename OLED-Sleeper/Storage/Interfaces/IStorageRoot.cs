namespace OLED_Sleeper.Storage.Interfaces
{
    // The single folder for settings, state files and logs.
    public interface IStorageRoot
    {
        // Only guaranteed to exist and be writable after IStorageRootPreparer.TryPrepare returned true.
        string DirectoryPath { get; }
    }
}
