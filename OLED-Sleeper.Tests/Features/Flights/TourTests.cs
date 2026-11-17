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
        public void CountryName_OfAnAirportInDutch()
        {
            Assert.Equal("Spanje", AirportCatalog.CountryName("TFS"));
            Assert.Equal("Nederland", AirportCatalog.CountryName("RTM"));
            Assert.Null(AirportCatalog.CountryName("EHXX"));
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
        public void Selection_TakesCountriesPlacesAndCodes()
        {
            var all = Sample.Concat(AirportCatalog.Parse(
                "TFS|ES|28.04|-16.57|3|Tenerife Sur Airport\nTFN|ES|28.48|-16.34|3|Tenerife Norte-Ciudad de La Laguna Airport\n" +
                "PMI|ES|39.55|2.74|3|Palma de Mallorca Airport\nMCZ|BR|-9.5|-35.8|3|Zumbi dos Palmares International Airport\n")).ToList();
            var lookup = Countries.Parse("NL|Nederland|Netherlands\nBE|België|Belgium\n").Lookup;

            var s = TourSelection.From("Nederland, Tenerife, palma, jfk, BRU, Atlantis", all, lookup);

            Assert.Equal(new[] { "AMS", "RTM", "EIN", "TFS", "TFN", "PMI", "BRU" }, s.Airports.Select(a => a.Code));
            Assert.Equal(new[] { "Nederland", "Tenerife (TFS, TFN)", "palma (PMI)", "BRU (BRU)" }, s.Recognised);
            Assert.Equal(new[] { "jfk", "Atlantis" }, s.Unknown); // not in this small list; "palma" is not "Palmares"
        }

        [Fact]
        public void PickAirport_EachTypedEntryGetsAnEqualTurn()
        {
            var big = Enumerable.Range(0, 50).Select(i => new TourAirport($"U{i:00}", "US", 40, -100, 8, "x")).ToList();
            var small = new List<TourAirport> { new("TFS", "ES", 28, -16, 3, "Tenerife Sur") };
            var planner = new TourPlanner(new Random(9));
            int tenerife = Enumerable.Range(0, 300).Count(_ => planner.PickAirport(new IReadOnlyList<TourAirport>[] { big, small }).Code == "TFS");
            Assert.InRange(tenerife, 90, 210); // about half, not 1 in 51
        }

        [Fact]
        public void Selection_BuiltInList_FindsTenerife()
        {
            var s = TourSelection.From("Nederland, Amerika, Tenerife");
            Assert.Empty(s.Unknown);
            Assert.Contains(s.Airports, a => a.Code == "TFS");
            Assert.Contains(s.Airports, a => a.Code == "JFK");
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
        public void QuietAirports_AreAvoidedWithinTheirPlace_ForAWhile()
        {
            var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var planner = new TourPlanner(new Random(2)) { Now = () => now };
            var frankfurt = new List<TourAirport> { new("FRA", "DE", 50, 8, 8, "Frankfurt Main"), new("HHN", "DE", 49.9, 7.3, 3, "Frankfurt-Hahn") };
            planner.MarkQuiet("HHN");
            Assert.All(Enumerable.Range(0, 6), _ => Assert.Equal("FRA", planner.PickAirport(frankfurt).Code));
            Assert.Equal("FRA", planner.Alternative(new IReadOnlyList<TourAirport>[] { frankfurt }, frankfurt[1])!.Code);

            now += TourPlanner.QuietFor;
            Assert.False(planner.IsQuiet("HHN"));
        }

        [Fact]
        public void QuietPlace_StillGetsTurns_ButFewer()
        {
            var planner = new TourPlanner(new Random(4));
            var tenerife = new List<TourAirport> { new("TFS", "ES", 28, -16, 3, "Tenerife Sur") };
            var nl = new List<TourAirport> { new("AMS", "NL", 52, 4, 8, "Schiphol"), new("RTM", "NL", 51.9, 4.4, 3, "Rotterdam") };
            var be = new List<TourAirport> { new("BRU", "BE", 50.9, 4.5, 8, "Brussels") };
            var groups = new IReadOnlyList<TourAirport>[] { nl, tenerife, be };
            planner.MarkQuiet("TFS");
            Assert.Null(planner.Alternative(groups, tenerife[0])); // no other airport there: shown anyway
            var picks = Enumerable.Range(0, 600).Select(_ => planner.PickAirport(groups).Code).ToList();
            int tfs = picks.Count(c => c == "TFS");
            Assert.InRange(tfs, 40, 170);                  // fewer than a third (200), but not never
            Assert.Contains("RTM", picks);
        }

        [Fact]
        public void ActiveScreens_AreTheScreensShowingThePlanes()
        {
            using (FlightTour.Attach(null))
            using (var right = FlightTour.Attach(1))
            {
                Assert.Equal(new[] { 1 }, FlightTour.ActiveScreens); // only the right screen is asleep
                using (FlightTour.Attach(0))
                    Assert.Equal(new[] { 0, 1 }, FlightTour.ActiveScreens);
                Assert.Equal(new[] { 1 }, FlightTour.ActiveScreens);
                right.Dispose();
                right.Dispose(); // twice is harmless
                Assert.Empty(FlightTour.ActiveScreens);
            }
        }

        [Fact]
        public void Flying_CountsOnlyPlanesInTheAir()
        {
            var t = DateTime.UtcNow;
            var reports = new[]
            {
                new AircraftReport("a", "KLM1", 52, 4, 0, 300, 20000, false, t),
                new AircraftReport("b", "KLM2", 52, 4, 0, 10, null, true, t),     // parked
                new AircraftReport("c", "KLM3", 52, 4, 0, 20, 0, false, t),       // taxiing, no ground flag
                new AircraftReport("d", "KLM4", 52, 4, 0, 140, 1200, false, t),   // landing
            };
            Assert.Equal(2, TourPlanner.Flying(reports));
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
