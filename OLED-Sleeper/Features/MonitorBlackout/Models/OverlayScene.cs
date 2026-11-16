namespace OLED_Sleeper.Features.MonitorBlackout.Models
{
    /// <summary>What an overlay shows once the screen has faded to black.</summary>
    public enum OverlayScene
    {
        /// <summary>Just black.</summary>
        Black,

        /// <summary>The night sky with stars, planets and shooting stars.</summary>
        Stars,

        /// <summary>A dim map around a pin with the real aircraft flying over it.</summary>
        Flights
    }
}
