using OLED_Sleeper.Features.Sky;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OLED_Sleeper.Tests.Features.Sky
{
    public class SkyViewRenderTests
    {
        /// <summary>
        /// Renders one frame of the night sky. Always checks that rendering works and that the picture is
        /// mostly black (OLED-friendly). With SKY_SNAPSHOT=&lt;path.png&gt; set it also saves the frame.
        /// </summary>
        [Fact]
        public void Render_ProducesAMostlyBlackSkyWithSomeLight()
        {
            Exception? failure = null;
            double litFraction = 0, maxBrightness = 0;

            var thread = new Thread(() =>
            {
                try
                {
                    const int w = 2560, h = 1440;
                    var view = new SkyView();
                    view.Measure(new Size(w, h));
                    view.Arrange(new Rect(0, 0, w, h));
                    view.PrepareStillFrame();
                    view.InvalidateVisual();
                    view.UpdateLayout();

                    var bitmap = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(view);

                    var pixels = new byte[w * h * 4];
                    bitmap.CopyPixels(pixels, w * 4, 0);
                    long lit = 0;
                    for (int i = 0; i < pixels.Length; i += 4)
                    {
                        int v = Math.Max(pixels[i], Math.Max(pixels[i + 1], pixels[i + 2]));
                        if (v > 8) lit++;
                        maxBrightness = Math.Max(maxBrightness, v);
                    }
                    litFraction = (double)lit / (w * h);

                    var path = Environment.GetEnvironmentVariable("SKY_SNAPSHOT");
                    if (!string.IsNullOrEmpty(path))
                    {
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        using var file = System.IO.File.Create(path);
                        encoder.Save(file);
                    }
                }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            Assert.Null(failure);
            Assert.True(maxBrightness > 100, "er zou iets zichtbaar moeten zijn");
            Assert.True(litFraction < 0.05, $"te veel pixels aan: {litFraction:P2}");
        }
    }
}
