using System.Globalization;
using System.IO;
using System.Reflection;

namespace OLED_Sleeper.Features.Sky
{
    /// <summary>
    /// The real night sky: stars brighter than a magnitude limit, the constellation stick figures and the
    /// constellation names, read from the embedded <c>Assets/Sky/sky.txt</c> (from d3-celestial, BSD-3).
    /// Positions are J2000 right ascension / declination in degrees.
    /// </summary>
    public sealed class SkyCatalog
    {
        public readonly record struct Star(double Ra, double Dec, double Magnitude, double ColorIndex);

        public readonly record struct Label(string Abbreviation, double Ra, double Dec, string Name);

        public IReadOnlyList<Star> Stars { get; }

        /// <summary>Constellation stick figures: each line is a polyline of (ra, dec) points.</summary>
        public IReadOnlyList<(string Abbreviation, (double Ra, double Dec)[] Points)> Lines { get; }

        public IReadOnlyList<Label> Labels { get; }

        private SkyCatalog(List<Star> stars, List<(string, (double, double)[])> lines, List<Label> labels)
        {
            Stars = stars;
            Lines = lines;
            Labels = labels;
        }

        private static readonly Lazy<SkyCatalog> Embedded = new(() =>
        {
            var assembly = Assembly.GetExecutingAssembly();
            var name = assembly.GetManifestResourceNames().First(n => n.EndsWith("sky.txt", StringComparison.OrdinalIgnoreCase));
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            return Parse(reader.ReadToEnd(), DefaultMaxMagnitude);
        });

        /// <summary>The catalog built into the app. Loaded once, on first use.</summary>
        public static SkyCatalog Default => Embedded.Value;

        /// <summary>Stars fainter than this are left out: only the brighter ones, so the sky stays calm.</summary>
        public const double DefaultMaxMagnitude = 4.0;

        public static SkyCatalog Parse(string text, double maxMagnitude = DefaultMaxMagnitude)
        {
            var stars = new List<Star>();
            var lines = new List<(string, (double, double)[])>();
            var labels = new List<Label>();
            var ci = CultureInfo.InvariantCulture;

            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length < 2 || line[0] == '#') continue;
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                switch (parts[0])
                {
                    case "S" when parts.Length >= 5:
                        var mag = double.Parse(parts[3], ci);
                        if (mag <= maxMagnitude)
                            stars.Add(new Star(double.Parse(parts[1], ci), double.Parse(parts[2], ci), mag, double.Parse(parts[4], ci)));
                        break;

                    case "L" when parts.Length >= 4:
                        lines.Add((parts[1], parts.Skip(2).Select(p =>
                        {
                            var xy = p.Split(',');
                            return (double.Parse(xy[0], ci), double.Parse(xy[1], ci));
                        }).ToArray()));
                        break;

                    case "N" when parts.Length >= 5:
                        labels.Add(new Label(parts[1], double.Parse(parts[2], ci), double.Parse(parts[3], ci), string.Join(' ', parts.Skip(4))));
                        break;
                }
            }

            return new SkyCatalog(stars, lines, labels);
        }
    }
}
