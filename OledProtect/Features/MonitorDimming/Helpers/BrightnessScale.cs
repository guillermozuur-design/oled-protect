namespace OledProtect.Features.MonitorDimming.Helpers
{
    /// <summary>
    /// Converts the percentages the settings work in to the raw values a panel's brightness VCP code expects.
    /// </summary>
    public static class BrightnessScale
    {
        private const int FullScale = 100;

        /// <summary>
        /// Maps <paramref name="percent"/> (clamped to 0-100) onto the range 0-<paramref name="maxBrightness"/>,
        /// rounding halves up. A panel that reported no range (0) or that already counts in percent (100) gets the
        /// percentage unchanged.
        /// </summary>
        public static uint ToRawBrightness(int percent, uint maxBrightness)
        {
            uint clamped = (uint)Math.Clamp(percent, 0, FullScale);
            if (maxBrightness is 0 or FullScale)
            {
                return clamped;
            }

            // Integer form of round(percent * max / 100): add half the divisor before dividing.
            ulong scaledTwice = 2UL * clamped * maxBrightness;
            return (uint)((scaledTwice + FullScale) / (2 * FullScale));
        }
    }
}
