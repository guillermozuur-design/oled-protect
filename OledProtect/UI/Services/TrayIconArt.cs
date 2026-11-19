using Serilog;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace OledProtect.UI.Services
{
    // The two tray images: the app icon, and the same icon with a pause badge for work mode.
    internal static class TrayIconArt
    {
        private const int Px = 32;
        private static readonly Uri AppIconUri = new("pack://application:,,,/Assets/icon.ico", UriKind.Absolute);

        // Null when the embedded resource cannot be read; the tray then keeps its default image.
        public static Icon? LoadAppIcon()
        {
            try
            {
                var resource = System.Windows.Application.GetResourceStream(AppIconUri)
                    ?? throw new InvalidOperationException("icon.ico is missing from the resources");
                using var stream = resource.Stream;
                return new Icon(stream, Px, Px);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Tray icon image could not be loaded.");
                return null;
            }
        }

        // An amber disc with two black bars in the bottom-right corner.
        public static Icon? WithPauseBadge(Icon appIcon)
        {
            try
            {
                using var canvas = new Bitmap(Px, Px);
                using (var gfx = Graphics.FromImage(canvas))
                {
                    gfx.SmoothingMode = SmoothingMode.AntiAlias;
                    gfx.DrawIcon(appIcon, new Rectangle(0, 0, Px, Px));

                    using var amber = new SolidBrush(Color.FromArgb(255, 176, 32));
                    gfx.FillEllipse(amber, 14, 14, 18, 18);
                    gfx.FillRectangle(Brushes.Black, 19, 18, 3, 10);
                    gfx.FillRectangle(Brushes.Black, 24, 18, 3, 10);
                }

                // Icon.FromHandle does not own the HICON; cloning gives an icon with its own copy.
                // The original handle is tiny and kept for the app's lifetime.
                using var borrowed = Icon.FromHandle(canvas.GetHicon());
                return (Icon)borrowed.Clone();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Kon pauze-icoon niet maken; het normale icoon wordt gebruikt.");
                return null;
            }
        }
    }
}
