namespace OledProtect.Infrastructure.Runtime.Interfaces
{
    public interface IApplicationShutdown
    {
        // Only requests the exit and returns right away; cleanup that must precede it has to be done first.
        void Shutdown();
    }
}
