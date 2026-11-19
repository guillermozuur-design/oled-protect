namespace OledProtect.Features.MonitorDimming.Services
{
    /// <summary>
    /// How hardware brightness changes are faded: the number of DDC/CI writes and the pause between them.
    /// </summary>
    public sealed record BrightnessFadeOptions(int Steps, TimeSpan StepDelay)
    {
        /// <summary>No fading: one direct write.</summary>
        public static BrightnessFadeOptions None { get; } = new(1, TimeSpan.Zero);

        /// <summary>The fade the app uses: about 0.7 seconds in 8 steps.</summary>
        public static BrightnessFadeOptions Default { get; } = new(8, TimeSpan.FromMilliseconds(90));
    }
}
