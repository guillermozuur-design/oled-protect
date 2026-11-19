namespace OledProtect.Features.MonitorIdleDetection.Models
{
    // Why a screen counts as in use right now; the first condition that holds wins.
    public enum ActivityReason
    {
        None,          // nothing keeps it awake
        MousePosition, // the pointer is on it
        ActiveWindow,  // the focused window overlaps it
        SystemInput,   // keyboard or mouse was used recently, anywhere

        /// <summary>An app the user chose ("apps die het scherm aan houden") is open on this screen or everywhere.</summary>
        KeepAwakeApp
    }
}
