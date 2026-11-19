using System.Windows.Input;

namespace OledProtect.UI.Commands
{
    // ICommand for an async handler. The command reports itself disabled while a run is in flight,
    // so a double click cannot start the same work twice.
    public class AsyncRelayCommand : ICommand
    {
        private readonly Func<Task> _work;
        private readonly Func<bool>? _guard;
        private bool _running;

        public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
        {
            ArgumentNullException.ThrowIfNull(execute);
            _work = execute;
            _guard = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter) => !_running && (_guard is null || _guard());

        // async void is unavoidable for ICommand.Execute; the flag is reset even when the work throws.
        public async void Execute(object? parameter)
        {
            SetRunning(true);
            try
            {
                await _work();
            }
            finally
            {
                SetRunning(false);
            }
        }

        private void SetRunning(bool running)
        {
            _running = running;
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
