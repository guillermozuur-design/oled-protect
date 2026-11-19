namespace OledProtect.Messaging.Interfaces
{
    // One implementation per command type, resolved from the DI container by the mediator.
    public interface ICommandHandler<TCommand> where TCommand : ICommand
    {
        Task HandleAsync(TCommand command);
    }
}
