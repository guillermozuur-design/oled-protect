namespace OledProtect.Features.BrightnessSchedule.Services
{
    /// <summary>
    /// Applies each monitor's brightness schedule in the background.
    /// </summary>
    public interface IBrightnessScheduleService
    {
        void Start();

        void Stop();

        /// <summary>Forgets what was applied, so the next check sets every scheduled monitor again.</summary>
        void Reapply();
    }
}
