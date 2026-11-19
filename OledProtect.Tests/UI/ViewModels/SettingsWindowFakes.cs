using OledProtect.UI.Services.Interfaces;
using OledProtect.UI.ViewModels;
using System.Collections.ObjectModel;
using System.Globalization;

namespace OledProtect.Tests.UI.ViewModels
{
    /// <summary>
    /// Workspace stand-in. Every call is logged as "build WxH" or "refresh WxH". By default a call
    /// answers at once with <see cref="Screens"/>; <see cref="Stall"/> and <see cref="Fail"/> change
    /// only the very next call.
    /// </summary>
    internal sealed class ScriptedWorkspace : IWorkspaceService
    {
        private Func<Task<ObservableCollection<MonitorLayoutViewModel>>>? _nextAnswer;

        public MonitorLayoutViewModel[] Screens { get; set; } = [];

        public List<string> Log { get; } = [];

        public TaskCompletionSource<ObservableCollection<MonitorLayoutViewModel>> Stall()
        {
            var stalled = new TaskCompletionSource<ObservableCollection<MonitorLayoutViewModel>>();
            _nextAnswer = () => stalled.Task;
            return stalled;
        }

        public void Fail(Exception reason) =>
            _nextAnswer = () => Task.FromException<ObservableCollection<MonitorLayoutViewModel>>(reason);

        public Task<ObservableCollection<MonitorLayoutViewModel>> BuildWorkspaceAsync(double canvasWidth, double canvasHeight) =>
            Answer("build", canvasWidth, canvasHeight);

        public Task<ObservableCollection<MonitorLayoutViewModel>> RefreshWorkspaceAsync(double canvasWidth, double canvasHeight) =>
            Answer("refresh", canvasWidth, canvasHeight);

        private Task<ObservableCollection<MonitorLayoutViewModel>> Answer(string kind, double width, double height)
        {
            Log.Add(string.Create(CultureInfo.InvariantCulture, $"{kind} {width}x{height}"));

            var special = _nextAnswer;
            _nextAnswer = null;
            return special?.Invoke() ?? Task.FromResult(new ObservableCollection<MonitorLayoutViewModel>(Screens));
        }
    }

    /// <summary>
    /// Save stand-in. When <see cref="Accepts"/> is true it behaves like the real service on success:
    /// each screen it received becomes the new saved baseline.
    /// </summary>
    internal sealed class ScriptedSaver : IMonitorSettingsSaveService
    {
        public bool Accepts { get; set; } = true;

        public List<IReadOnlyList<MonitorLayoutViewModel>> Received { get; } = [];

        public bool TrySave(IReadOnlyList<MonitorLayoutViewModel> monitors)
        {
            Received.Add(monitors);
            if (Accepts)
            {
                foreach (var screen in monitors)
                    screen.Configuration.MarkAsSaved();
            }
            return Accepts;
        }
    }
}
