namespace OLED_Sleeper.Storage.Interfaces
{
    // JSON persistence for small files that live directly in the storage root.
    public interface IAppDataFileStore
    {
        // Null/default when the file is absent or unreadable (callers fall back to their defaults).
        T? Read<T>(string fileName);

        // False when anything went wrong; the previous contents then stay in place.
        bool TryWrite<T>(string fileName, T value);
    }
}
