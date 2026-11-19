namespace OLED_Sleeper.Messaging.Interfaces
{
    // Lets a sender fire a command without knowing (or referencing) the class that carries it out.
    public interface IMediator
    {
        Task SendAsync<TCommand>(TCommand command) where TCommand : ICommand;
    }
}
