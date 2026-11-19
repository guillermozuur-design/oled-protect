namespace OLED_Sleeper.Infrastructure.Runtime.Interfaces
{
    public interface IApplicationShutdown
    {
        // Only requests the exit and returns right away; cleanup that must precede it has to be done first.
        void Shutdown();
    }
}
