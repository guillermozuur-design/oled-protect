using System.Windows;

namespace OLED_Sleeper.Native
{
    // Conversions from Win32 value types to their WPF counterparts.
    internal static class NativeTypeMapper
    {
        // Win32 stores the four edges; WPF wants an origin plus a size.
        public static Rect ToWindowsRect(this NativeMethods.Rect edges) =>
            new(edges.Left, edges.Top, edges.Right - edges.Left, edges.Bottom - edges.Top);
    }
}
