namespace OLED_Sleeper.Storage.Interfaces
{
    // Startup check that the storage folder can actually be used.
    public interface IStorageRootPreparer
    {
        // On failure, error is a user-facing (Dutch) message that names the folder; on success it is null.
        bool TryPrepare(out string? error);
    }
}
