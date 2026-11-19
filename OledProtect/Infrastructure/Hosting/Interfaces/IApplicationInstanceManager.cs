namespace OledProtect.Infrastructure.Hosting.Interfaces
{
    // Makes sure only one copy of the app runs; a second launch brings the first one's window forward.
    public interface IApplicationInstanceManager : IDisposable
    {
        // Meaningful only after Initialize.
        bool IsFirstInstance { get; }

        // Claims the single-instance lock, or (when another copy holds it) pokes that copy and exits.
        void Initialize();

        // What to run when another launch pokes us. Supplied later because the window only exists once DI is up.
        void SetShowMainWindowAction(Action showMainWindowAction);
    }
}
