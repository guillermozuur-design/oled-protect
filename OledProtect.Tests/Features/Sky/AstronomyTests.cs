using OledProtect.Features.Sky;

namespace OledProtect.Tests.Features.Sky
{
    public class AstronomyTests
    {
        private static double AngularSeparation((double Ra, double Dec) a, (double Ra, double Dec) b)
        {
            double d = Astronomy.Deg;
            double cos = Math.Sin(a.Dec * d) * Math.Sin(b.Dec * d) + Math.Cos(a.Dec * d) * Math.Cos(b.Dec * d) * Math.Cos((a.Ra - b.Ra) * d);
            return Math.Acos(Math.Clamp(cos, -1, 1)) / d;
        }

        [Fact]
        public void GreatConjunction2020_JupiterAndSaturnAreTogetherInCapricornus()
        {
            // 21 december 2020, 18:00 UTC: Jupiter en Saturnus 0,1 graad uit elkaar, RA ~20h08m, Dec ~-20,5.
            double d = Astronomy.DaysSinceJ2000(new DateTime(2020, 12, 21, 18, 0, 0, DateTimeKind.Utc));
            var jupiter = Astronomy.Planet(Astronomy.Body.Jupiter, d);
            var saturn = Astronomy.Planet(Astronomy.Body.Saturn, d);

            Assert.True(AngularSeparation(jupiter, saturn) < 0.5, $"scheiding {AngularSeparation(jupiter, saturn):F2}");
            Assert.InRange(jupiter.Ra, 300, 304);
            Assert.InRange(jupiter.Dec, -21.5, -19.5);
        }

        [Fact]
        public void MarchEquinox2000_SunIsAtTheVernalPoint()
        {
            double d = Astronomy.DaysSinceJ2000(new DateTime(2000, 3, 20, 7, 35, 0, DateTimeKind.Utc));
            var sun = Astronomy.Sun(d);
            Assert.True(AngularSeparation((sun.Ra, sun.Dec), (0, 0)) < 0.3);
        }

        [Fact]
        public void Polaris_StandsAtTheObserversLatitude()
        {
            // De Poolster staat altijd (bijna) op een hoogte gelijk aan je breedtegraad, in het noorden.
            double d = Astronomy.DaysSinceJ2000(new DateTime(2026, 11, 16, 21, 0, 0, DateTimeKind.Utc));
            var (e, n, u) = Astronomy.ToHorizon(37.95, 89.26, Astronomy.LocalSiderealTime(d, 5.1), 52.1);
            double altitude = Math.Asin(u) / Astronomy.Deg;
            Assert.InRange(altitude, 51.2, 53.0);
            Assert.True(n > 0);
        }

        [Fact]
        public void NewMoon_IsAlmostDark_FullMoon_IsAlmostFullyLit()
        {
            // Nieuwe maan 2024-04-08 18:21 UTC (zonsverduistering), volle maan 2024-04-23 23:49 UTC.
            var nieuw = Astronomy.Moon(Astronomy.DaysSinceJ2000(new DateTime(2024, 4, 8, 18, 21, 0, DateTimeKind.Utc)));
            var vol = Astronomy.Moon(Astronomy.DaysSinceJ2000(new DateTime(2024, 4, 23, 23, 49, 0, DateTimeKind.Utc)));
            Assert.True(nieuw.Illuminated < 0.02, $"nieuw {nieuw.Illuminated:F3}");
            Assert.True(vol.Illuminated > 0.98, $"vol {vol.Illuminated:F3}");
        }

        [Fact]
        public void EmbeddedCatalog_ContainsTheBrightStarsAndAllConstellations()
        {
            var catalog = SkyCatalog.Default;
            Assert.InRange(catalog.Stars.Count, 350, 700);
            Assert.Contains(catalog.Stars, s => s.Magnitude < -1.4); // Sirius
            Assert.Equal(89, catalog.Labels.Count);
            Assert.Contains(catalog.Labels, l => l.Name == "Grote Beer");
            Assert.True(catalog.Lines.Count > 100);
        }
    }
}
