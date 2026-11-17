using OLED_Sleeper.Features.Flights;

namespace OLED_Sleeper.Tests.Features.Flights
{
    public class TourTests
    {
        private static readonly List<TourAirport> Sample = AirportCatalog.Parse(
            "# note\nAMS|NL|52.3086|4.7639|3|Amsterdam Airport Schiphol\nRTM|NL|51.9569|4.4372|3|Rotterdam The Hague Airport\n" +
            "EIN|NL|51.4501|5.3745|1|Eindhoven Airport\nBRU|BE|50.9014|4.4844|3|Brussels Airport\nbroken|line\n");

        [Fact]
        public void Catalog_ParsesLinesAndWeighsHubsMost()
        {
            Assert.Equal(4, Sample.Count);
            Assert.Equal(AirportCatalog.HubWeight, Sample.Single(a => a.Code == "AMS").Weight);
            Assert.Equal(3, Sample.Single(a => a.Code == "RTM").Weight);
            Assert.Equal(1, Sample.Single(a => a.Code == "EIN").Weight);
        }

        [Fact]
        public void Catalog_BuiltInListIsThere()
        {
            Assert.True(AirportCatalog.All.Count > 1000);
            Assert.Contains(AirportCatalog.All, a => a.Code == "AMS" && a.Country == "NL");
            Assert.Contains(Countries.All, c => c.Code == "NL" && c.Name == "Nederland");
        }

        [Fact]
        public void Countries_AreRecognisedInDutchEnglishAndShortForms()
        {
            var (known, unknown) = Countries.Read("Nederland, belgie; Spain en Portugal / UK, Atlantis");
            Assert.Equal(new[] { "NL", "BE", "ES", "PT", "GB" }, known.Select(c => c.Code));
            Assert.Equal(new[] { "Atlantis" }, unknown);
            Assert.Empty(Countries.Read("").Known);
        }

        [Fact]
        public void AirportsIn_OnlyThoseCountries_OrAllWhenNone()
        {
            Assert.Equal(new[] { "BRU" }, TourPlanner.AirportsIn(Sample, new[] { "BE" }).Select(a => a.Code));
            Assert.Equal(4, TourPlanner.AirportsIn(Sample, new[] { "XX" }).Count);
        }

        [Fact]
        public void AirportsIn_SmallFieldsOnlyWhenACountryHasFewBigOnes()
        {
            var more = Sample.Append(new TourAirport("GRQ", "NL", 53.1, 6.6, 3, "Groningen Airport Eelde")).ToList();
            Assert.Contains(TourPlanner.AirportsIn(Sample, new[] { "NL" }), a => a.Code == "EIN");    // 2 big: medium kept
            Assert.DoesNotContain(TourPlanner.AirportsIn(more, new[] { "NL" }), a => a.Code == "EIN"); // 3 big: only those
        }

        [Fact]
        public void PickAirport_DoesNotRepeatRightAway()
        {
            var planner = new TourPlanner(new Random(3));
            var picks = Enumerable.Range(0, 4).Select(_ => planner.PickAirport(Sample).Code).ToList();
            Assert.Equal(4, picks.Distinct().Count());
        }

        [Fact]
        public void PickAirport_BusyOnesComeMoreOften()
        {
            var planner = new TourPlanner(new Random(5));
            var counts = new Dictionary<string, int>();
            // Two airports only: the "not again right away" rule then alternates less strictly.
            var two = Sample.Where(a => a.Code is "AMS" or "EIN").ToList();
            var single = new[] { two[0] }.ToList();
            for (int i = 0; i < 2000; i++)
            {
                var p = new TourPlanner(new Random(i)).PickAirport(two).Code;
                counts[p] = counts.GetValueOrDefault(p) + 1;
            }
            Assert.True(counts["AMS"] > counts["EIN"] * 4);
            Assert.Equal("AMS", planner.PickAirport(single).Code);
            Assert.Equal("AMS", planner.PickAirport(single).Code); // only one: it may come again
        }

        [Theory]
        [InlineData(0, true, false)]
        [InlineData(2, true, true)]
        [InlineData(5, true, true)]
        [InlineData(2, false, false)]
        public void EveryThirdSceneFollowsAFlight(int number, bool follow, bool expected) =>
            Assert.Equal(expected, TourPlanner.IsFollowTurn(number, follow));

        [Fact]
        public void PickFlight_TakesAFlyingAirlinerNearby_PreferringAKnownRoute()
        {
            var t = DateTime.UtcNow;
            var reports = new[]
            {
                new AircraftReport("a", "KLM1", 52.30, 4.76, 90, 300, 20000, false, t),   // ok, no route
                new AircraftReport("b", "EZY2AB", 52.32, 4.70, 90, 400, 30000, false, t), // ok, route known
                new AircraftReport("c", "PHABC", 52.30, 4.76, 90, 120, 2000, false, t),   // private, slow
                new AircraftReport("d", "KLM3", 52.30, 4.76, 0, 0, null, true, t),        // on the ground
                new AircraftReport("e", "KLM4", 54.00, 4.76, 90, 400, 30000, false, t),   // far away
            };
            var planner = new TourPlanner(new Random(1));
            Assert.Equal("b", planner.PickFlight(reports, 52.31, 4.76, 30, cs => cs == "EZY2AB")!.Hex);
            Assert.Contains(planner.PickFlight(reports, 52.31, 4.76, 30, _ => false)!.Hex, new[] { "a", "b" });
            Assert.Null(planner.PickFlight(reports.Skip(2), 52.31, 4.76, 30, _ => true));
        }

        [Fact]
        public void Scene_FadesInAndOut_HomeAlwaysVisible()
        {
            var start = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            var scene = new TourScene(1, SceneKind.Airport, 52, 4, 22, start, start.AddSeconds(20), 1, "AMS");
            Assert.Equal(0, scene.Visibility(start, 1.2));
            Assert.Equal(0.5, scene.Visibility(start.AddSeconds(0.6), 1.2), 3);
            Assert.Equal(1, scene.Visibility(start.AddSeconds(10), 1.2));
            Assert.Equal(0, scene.Visibility(start.AddSeconds(20), 1.2));
            var home = scene with { Kind = SceneKind.Home };
            Assert.Equal(1, home.Visibility(start, 1.2));
        }
    }
}
