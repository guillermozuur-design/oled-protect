using OledProtect.Messaging.Interfaces;

namespace OledProtect.Tests.TestDoubles
{
    // Mediator that only writes down what it is sent; chosen command types can be made to fail. Thread-safe,
    // so it can also sit behind background loops.
    public sealed class RecordingMediator : IMediator
    {
        private readonly List<ICommand> _sent = [];
        private readonly Dictionary<Type, Exception> _failures = [];

        public IReadOnlyList<ICommand> Sent
        {
            get
            {
                lock (_sent)
                {
                    return [.. _sent];
                }
            }
        }

        public List<T> SentOf<T>() where T : ICommand => [.. Sent.OfType<T>()];

        public RecordingMediator Failing<T>(Exception error) where T : ICommand
        {
            lock (_sent)
            {
                _failures[typeof(T)] = error;
            }

            return this;
        }

        public Task SendAsync<TCommand>(TCommand command) where TCommand : ICommand
        {
            Exception? error;
            lock (_sent)
            {
                _sent.Add(command);
                _failures.TryGetValue(command.GetType(), out error);
            }

            return error is null ? Task.CompletedTask : Task.FromException(error);
        }
    }
}
