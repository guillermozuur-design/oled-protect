using OledProtect.Infrastructure.Hosting;
using System.Windows;

namespace OledProtect
{
    // Deliberately thin: every lifecycle decision lives in ApplicationBootstrapper.
    public partial class App : Application
    {
        private ApplicationBootstrapper? _host;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Logoff/shutdown: restore the monitors while Windows still gives us time.
            SessionEnding += (_, _) => _host?.ShutdownApp();

            _host = new ApplicationBootstrapper(e.Args);
            _host.Initialize();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_host is { } host)
            {
                host.ShutdownApp();
                host.Dispose();
            }

            base.OnExit(e);
        }
    }
}
