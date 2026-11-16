namespace OLED_Sleeper.Features.WorkMode.Services
{
    /// <summary>
    /// Werkmodus: zolang die aan staat, blijven alle schermen gewoon aan (geen blackout, dim of screensaver).
    /// Handig bij video kijken of als je echt beide schermen nodig hebt.
    /// </summary>
    public interface IWorkModeService
    {
        bool IsEnabled { get; }

        /// <summary>Fires with the new state whenever the work mode is switched.</summary>
        event Action<bool>? Changed;

        void SetEnabled(bool enabled);

        void Toggle();
    }
}
