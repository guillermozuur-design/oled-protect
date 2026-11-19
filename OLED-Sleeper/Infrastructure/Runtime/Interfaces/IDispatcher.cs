namespace OLED_Sleeper.Infrastructure.Runtime.Interfaces
{
    // Abstraction over the WPF dispatcher so services can hop to the UI thread and still be unit tested.
    public interface IDispatcher
    {
        bool IsOnUiThread { get; }

        // Blocking; executes inline when already on the UI thread.
        void Invoke(Action action);

        // Queues the work; the task completes when it has run.
        Task InvokeAsync(Action action);

        // Queues at a priority below input, so pending clicks/keys are handled first. Used before showing a
        // modal prompt, so the very click that triggered it cannot also dismiss it.
        Task InvokeAfterInputAsync(Action action);
    }
}
