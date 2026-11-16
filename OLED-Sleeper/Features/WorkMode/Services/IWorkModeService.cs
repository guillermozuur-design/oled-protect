namespace OLED_Sleeper.Features.WorkMode.Services
{
    /// <summary>
    /// Werkmodus: zolang die aan staat, blijven alle schermen gewoon aan (geen blackout, dim of screensaver).
    /// Handig bij video kijken of als je echt beide schermen nodig hebt. Kan ook tijdelijk (bv. 1 uur).
    /// </summary>
    public interface IWorkModeService
    {
        bool IsEnabled { get; }

        /// <summary>When the work mode switches itself off again (UTC), or null when it stays on until switched off.</summary>
        DateTime? EnabledUntil { get; }

        /// <summary>Fires with the new state whenever the work mode is switched or its end time changes.</summary>
        event Action<bool>? Changed;

        void SetEnabled(bool enabled);

        /// <summary>Switches the work mode on for <paramref name="duration"/>, after which protection returns by itself.</summary>
        void EnableFor(TimeSpan duration);

        void Toggle();
    }
}
