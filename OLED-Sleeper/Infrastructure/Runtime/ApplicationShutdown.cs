using OLED_Sleeper.Infrastructure.Runtime.Interfaces;
using System.Diagnostics.CodeAnalysis;
using System.Windows;

namespace OLED_Sleeper.Infrastructure.Runtime
{
    [ExcludeFromCodeCoverage]
    public sealed class ApplicationShutdown : IApplicationShutdown
    {
        // Application.Current is null once WPF has torn down; there is nothing left to stop then.
        public void Shutdown()
        {
            if (Application.Current is { } app)
                app.Shutdown();
        }
    }
}
