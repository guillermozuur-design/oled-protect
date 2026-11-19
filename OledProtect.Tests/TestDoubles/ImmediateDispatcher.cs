using OledProtect.Infrastructure.Runtime.Interfaces;

namespace OledProtect.Tests.TestDoubles
{
    // Synchronous IDispatcher: work runs inline and every entry point is counted, so tests can check that
    // code marshalled to the "UI thread" without having one.
    public class ImmediateDispatcher : IDispatcher
    {
        // Guards against code that keeps re-dispatching itself: fail the test, do not blow the stack.
        private const int DepthLimit = 8;

        private int _depth;
        private int _sync;
        private int _queued;
        private int _afterInput;
        private bool _uiThread = true;

        public int InvokeCount => _sync;

        public int InvokeAsyncCount => _queued;

        public int AfterInputCount => _afterInput;

        // Defaults to true. Set false to push the code under test onto its marshalling path; it always
        // reads true while a dispatched action runs.
        public bool IsOnUiThread
        {
            get => _uiThread;
            set => _uiThread = value;
        }

        public void Invoke(Action action) => Dispatch(ref _sync, action);

        public Task InvokeAsync(Action action) => Dispatch(ref _queued, action);

        public Task InvokeAfterInputAsync(Action action) => Dispatch(ref _afterInput, action);

        private Task Dispatch(ref int counter, Action action)
        {
            counter++;
            if (_depth == DepthLimit)
                throw new InvalidOperationException("Dispatcher re-entered " + DepthLimit + " times; likely an endless loop.");

            bool callerState = _uiThread;
            _depth++;
            _uiThread = true;
            try
            {
                action();
            }
            finally
            {
                _uiThread = callerState;
                _depth--;
            }

            return Task.CompletedTask;
        }
    }
}
