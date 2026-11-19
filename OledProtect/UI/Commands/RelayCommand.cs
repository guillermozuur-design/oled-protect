using System.Windows.Input;

namespace OledProtect.UI.Commands
{
    // ICommand over delegates. Enabled state is re-queried by WPF's CommandManager (focus changes,
    // input), so view models do not have to raise CanExecuteChanged themselves.
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _run;
        private readonly Func<object?, bool> _isEnabled;

        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            ArgumentNullException.ThrowIfNull(execute);
            _run = execute;
            _isEnabled = canExecute is null ? AlwaysEnabled : arg => canExecute(arg);
        }

        // For handlers that ignore the command parameter.
        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            ArgumentNullException.ThrowIfNull(execute);
            _run = _ => execute();
            _isEnabled = canExecute is null ? AlwaysEnabled : _ => canExecute();
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter) => _isEnabled(parameter);

        public void Execute(object? parameter) => _run(parameter);

        private static bool AlwaysEnabled(object? _) => true;
    }
}
