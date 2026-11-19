using System.Windows;

namespace OledProtect.Features.MonitorBlackout.Services.Interfaces
{
    // One screen cover. UI thread only.
    public interface IOverlayWindow
    {
        // The Win32 window that represents this overlay; zero before Show and after Close.
        nint Handle { get; }

        // Covers the given physical-pixel rectangle without stealing focus.
        void Show(Rect bounds);

        void Close();
    }
}
