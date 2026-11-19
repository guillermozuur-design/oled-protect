using OledProtect.Infrastructure.Runtime.Interfaces;

namespace OledProtect.Tests.TestDoubles
{
    // IDispatcher without a UI thread: every call runs on the spot and is written to a journal, so a test can
    // see which entry point the code under test used and how often.
    public sealed class ImmediateDispatcher : IDispatcher
    {
        public enum Entry
        {
            Invoke,
            InvokeAsync,
            InvokeAfterInput,
        }

        // Code that keeps scheduling itself would otherwise end in a stack overflow instead of a failed test.
        private const int MaxNesting = 8;

        private readonly List<Entry> _journal = [];
        private int _nesting;
        private bool _callerIsUi = true;

        public IReadOnlyList<Entry> Journal => _journal;

        public int InvokeCount => CountOf(Entry.Invoke);

        public int InvokeAsyncCount => CountOf(Entry.InvokeAsync);

        public int AfterInputCount => CountOf(Entry.InvokeAfterInput);

        // What the caller looks like (true unless a test says otherwise). Inside dispatched work it is always true.
        public bool IsOnUiThread
        {
            get => _nesting > 0 || _callerIsUi;
            set => _callerIsUi = value;
        }

        public void Invoke(Action action) => RunNow(Entry.Invoke, action);

        public Task InvokeAsync(Action action)
        {
            RunNow(Entry.InvokeAsync, action);
            return Task.CompletedTask;
        }

        public Task InvokeAfterInputAsync(Action action)
        {
            RunNow(Entry.InvokeAfterInput, action);
            return Task.CompletedTask;
        }

        private int CountOf(Entry entry) => _journal.Count(e => e == entry);

        private void RunNow(Entry entry, Action action)
        {
            _journal.Add(entry);
            if (_nesting >= MaxNesting)
            {
                throw new InvalidOperationException($"Dispatched work nested {_nesting} deep; the code under test probably loops.");
            }

            _nesting++;
            try
            {
                action();
            }
            finally
            {
                _nesting--;
            }
        }
    }
}
