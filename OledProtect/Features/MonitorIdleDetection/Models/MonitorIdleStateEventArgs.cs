using OledProtect.Features.UserSettings.Models;
using System.Windows;

namespace OledProtect.Features.MonitorIdleDetection.Models
{
    // Snapshot of one screen at the moment it went idle or woke up, carried inside the behaviour commands.
    public class MonitorIdleStateEventArgs(
        string hardwareId,
        int displayNumber,
        Rect bounds,
        MonitorSettings settings,
        nint foregroundWindowHandle,
        ActivityReason reason) : EventArgs
    {
        public string HardwareId => hardwareId;

        public int DisplayNumber => displayNumber;

        // Physical screen pixels.
        public Rect Bounds => bounds;

        public MonitorSettings Settings => settings;

        // The focused window when the snapshot was taken; lets a handler recognise our own overlay.
        public nint ForegroundWindowHandle => foregroundWindowHandle;

        public ActivityReason Reason => reason;

        // Written by a handler to veto the transition: idle detection then keeps the screen's state as is.
        public bool IsIgnored { get; set; }
    }
}
