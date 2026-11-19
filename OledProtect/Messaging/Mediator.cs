using OledProtect.Messaging.Interfaces;
using Serilog;

namespace OledProtect.Messaging
{
    // Routes each command to the handler registered for its exact type. Handlers are looked up per send, so
    // transient handlers get a fresh instance every time.
    public sealed class Mediator : IMediator
    {
        private readonly IServiceProvider _services;

        public Mediator(IServiceProvider serviceProvider)
        {
            ArgumentNullException.ThrowIfNull(serviceProvider);
            _services = serviceProvider;
        }

        // Kept async on purpose: several callers fire and forget, so every failure (bad argument, missing
        // handler, handler crash) has to surface through the returned task, never as a synchronous throw.
        public async Task SendAsync<TCommand>(TCommand command) where TCommand : ICommand
        {
            ArgumentNullException.ThrowIfNull(command);
            await ResolveHandler<TCommand>().HandleAsync(command);
        }

        private ICommandHandler<TCommand> ResolveHandler<TCommand>() where TCommand : ICommand
        {
            if (_services.GetService(typeof(ICommandHandler<TCommand>)) is ICommandHandler<TCommand> handler)
            {
                return handler;
            }

            // A missing registration is a wiring bug; make it loud in the log as well as to the caller.
            string problem = $"Cannot send {typeof(TCommand).Name}: nothing is registered to handle it.";
            Log.Error(problem);
            throw new InvalidOperationException(problem);
        }
    }
}
