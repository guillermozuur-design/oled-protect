using OledProtect.Infrastructure.Runtime.Interfaces;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Threading;

namespace OledProtect.Infrastructure.Runtime
{
    // Production IDispatcher backed by the dispatcher of the running WPF Application.
    [ExcludeFromCodeCoverage]
    public sealed class ApplicationDispatcher : IDispatcher
    {
        private static Dispatcher Ui => Application.Current.Dispatcher;

        public bool IsOnUiThread => Ui.CheckAccess();

        public void Invoke(Action action) => Ui.Invoke(action);

        public Task InvokeAsync(Action action) => Ui.InvokeAsync(action).Task;

        // Background priority sits below Input, which is what lets queued input go first.
        public Task InvokeAfterInputAsync(Action action) =>
            Ui.InvokeAsync(action, DispatcherPriority.Background).Task;
    }
}
