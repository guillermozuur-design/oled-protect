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
                    Point? meteorHead = null;
                    const int w = 2560, h = 1440, screens = 2, gap = 86; // 2 cm bij 43 px/cm
                    var visual = new DrawingVisual();
                    using (var dcAll = visual.RenderOpen())
                    {
                        for (int s = 0; s < screens; s++)
                        {
                            var view = new SkyView { PanoramaIndex = s, PanoramaCount = screens, GapPixels = gap };
                            view.Measure(new Size(w, h));
                            view.Arrange(new Rect(0, 0, w, h));
                            view.PrepareStillFrame();
                            view.InvalidateVisual();
                            view.UpdateLayout();
                            var part = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
                            part.Render(view);
                            dcAll.DrawImage(part, new Rect(s * (w + gap), 0, w, h));
                            foreach (var head in view.StillFrameMeteorHeads)
                                if (head.X >= 0 && head.X <= w) meteorHead ??= new Point(head.X + s * (w + gap), head.Y);
                            if (s > 0) dcAll.DrawRectangle(new SolidColorBrush(Color.FromRgb(40, 40, 40)), null, new Rect(s * (w + gap) - gap, 0, gap, h)); // de schermrand
                        }
                    }
                    var bitmap = new RenderTargetBitmap(w * screens + gap * (screens - 1), h, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(visual);

                    int bw = bitmap.PixelWidth;
                    var pixels = new byte[bw * h * 4];
                    bitmap.CopyPixels(pixels, bw * 4, 0);
                    long lit = 0;
                    for (int i = 0; i < pixels.Length; i += 4)
                    {
                        int v = Math.Max(pixels[i], Math.Max(pixels[i + 1], pixels[i + 2]));
                        if (v > 8) lit++;
                        maxBrightness = Math.Max(maxBrightness, v);
                    }
                    litFraction = (double)lit / (bw * h);

                    var path = Environment.GetEnvironmentVariable("SKY_SNAPSHOT");
                    if (!string.IsNullOrEmpty(path))
                    {
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        using var file = System.IO.File.Create(path);
                        encoder.Save(file);

                        // Plus a 1:1 crop around a shooting star, to judge it at real size.
                        if (meteorHead is Point mh)
                        {
                            int cw = 1000, ch = 560;
                            int cx = (int)Math.Clamp(mh.X - cw / 2.0, 0, bitmap.PixelWidth - cw);
                            int cy = (int)Math.Clamp(mh.Y - ch / 2.0, 0, h - ch);
                            var crop = new CroppedBitmap(bitmap, new Int32Rect(cx, cy, cw, ch));
                            var cropEncoder = new PngBitmapEncoder();
                            cropEncoder.Frames.Add(BitmapFrame.Create(crop));
                            using var cropFile = System.IO.File.Create(System.IO.Path.ChangeExtension(path, null) + "_meteor.png");
                            cropEncoder.Save(cropFile);
                        }
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
