using OledProtect.Features.Flights;

namespace OledProtect.Tests.Features.Flights
{
    public class VectorTileTests
    {
        // ---- a tiny protobuf writer, to build test tiles ----
        private static void Varint(List<byte> b, ulong v)
        {
            while (v >= 0x80) { b.Add((byte)(v | 0x80)); v >>= 7; }
            b.Add((byte)v);
        }

        private static void Key(List<byte> b, int field, int wire) => Varint(b, (ulong)(field << 3 | wire));

        private static void Bytes(List<byte> b, int field, IReadOnlyCollection<byte> data)
        {
            Key(b, field, 2);
            Varint(b, (ulong)data.Count);
            b.AddRange(data);
        }

        private static void Text(List<byte> b, int field, string s) => Bytes(b, field, System.Text.Encoding.UTF8.GetBytes(s));

        private static List<byte> Packed(IEnumerable<uint> values)
        {
            var b = new List<byte>();
            foreach (var v in values) Varint(b, v);
            return b;
        }

        private static uint Zig(int v) => (uint)((v << 1) ^ (v >> 31));

        private static byte[] Tile(string layer, string cls, int type, uint[] geometry, int extent = 4096)
        {
            var feature = new List<byte>();
            Bytes(feature, 2, Packed(new uint[] { 0, 0 }));          // tags: class = value 0
            Key(feature, 3, 0); Varint(feature, (ulong)type);
            Bytes(feature, 4, Packed(geometry));

            var value = new List<byte>();
            Text(value, 1, cls);

            var l = new List<byte>();
            Text(l, 1, layer);
            Bytes(l, 2, feature);
            Text(l, 3, "class");
            Bytes(l, 4, value);
            Key(l, 5, 0); Varint(l, (ulong)extent);

            var tile = new List<byte>();
            Bytes(tile, 3, l);
            return tile.ToArray();
        }

        [Fact]
        public void Decode_ReadsLayerTagsAndLineGeometry()
        {
            // MoveTo(100,100) LineTo(+100,0) (0,+200)
            var data = Tile("transportation", "motorway", 2, new[] { 9u, Zig(100), Zig(100), 18u, Zig(100), Zig(0), Zig(0), Zig(200) });

            var features = VectorTile.Decode(data);

            var f = Assert.Single(features);
            Assert.Equal("transportation", f.Layer);
            Assert.Equal("motorway", f.Tags["class"]);
            Assert.Equal(VectorTile.GeometryType.Line, f.Type);
            Assert.Equal(new[] { (100.0, 100.0), (200.0, 100.0), (200.0, 300.0) }, f.Parts.Single());
        }

        [Fact]
        public void Decode_SkipsLayersThatAreNotAsked()
        {
            var data = Tile("building", "house", 3, new[] { 9u, 0u, 0u });
            Assert.Empty(VectorTile.Decode(data, new HashSet<string> { "transportation" }));
        }

        [Fact]
        public void DecodeGeometry_ClosesPolygonRings()
        {
            // MoveTo(0,0) LineTo(10,0)(0,10) ClosePath
            var parts = VectorTile.DecodeGeometry(new[] { 9u, 0u, 0u, 18u, Zig(10), 0u, 0u, Zig(10), 15u }, VectorTile.GeometryType.Polygon);
            var ring = Assert.Single(parts);
            Assert.Equal(ring[0], ring[^1]);
            Assert.Equal(4, ring.Count);
        }

        [Fact]
        public void DecodeGeometry_MultiPointGivesOnePartPerPoint()
        {
            var parts = VectorTile.DecodeGeometry(new[] { 17u, Zig(5), Zig(5), Zig(3), Zig(-2) }, VectorTile.GeometryType.Point);
            Assert.Equal(new[] { (5.0, 5.0), (8.0, 3.0) }, parts.Select(p => p.Single()));
        }

        [Fact]
        public void Decode_GarbageDoesNotThrow()
        {
            var random = new Random(1);
            for (int i = 0; i < 200; i++)
            {
                var bytes = new byte[random.Next(1, 64)];
                random.NextBytes(bytes);
                var ex = Record.Exception(() => VectorTile.Decode(bytes));
                Assert.True(ex == null || ex is ArgumentException or IndexOutOfRangeException or ArgumentOutOfRangeException,
                    $"unexpected {ex?.GetType().Name}");
            }
        }
    }

    public class MapMathTests
    {
        [Fact]
        public void TileAt_UtrechtAtZoom10()
        {
            Assert.Equal((526, 337), TileMath.TileAt(52.09, 5.12, 10));
        }

        [Fact]
        public void TileCornersRoundTrip()
        {
            var (lat, lon) = TileMath.ToLatLon(10, 526, 337, 0.5, 0.5);
            Assert.Equal((526, 337), TileMath.TileAt(lat, lon, 10));
        }

        [Fact]
        public void LocalProjection_KeepsRealDistances()
        {
            var p = new LocalProjection(52.09, 5.12);
            Assert.Equal((0.0, 0.0), p.ToKm(52.09, 5.12));

            // Utrecht → Amsterdam Centraal is about 35 km, mostly north (y negative = north).
            var (x, y) = p.ToKm(52.379, 4.900);
            Assert.InRange(Math.Sqrt(x * x + y * y), 33, 37);
            Assert.True(y < -30);

            var back = p.ToLatLon(x, y);
            Assert.Equal(52.379, back.Lat, 6);
            Assert.Equal(4.900, back.Lon, 6);
        }

        [Fact]
        public void ClipLine_CutsAtTheEdgesAndSplitsWhenLeavingAndReturning()
        {
            var line = new List<(double X, double Y)> { (-10, 5), (5, 5), (20, 5), (20, 15), (5, 8), (5, 6) };
            var pieces = MapBuilder.ClipLine(line, 0, 10);

            Assert.Equal(2, pieces.Count);
            Assert.Equal((0.0, 5.0), pieces[0][0]);
            Assert.Equal((10.0, 5.0), pieces[0][^1]);
            Assert.All(pieces.SelectMany(p => p), pt => Assert.InRange(pt.X, 0, 10));
            Assert.Equal((5.0, 6.0), pieces[1][^1]);
        }

        [Fact]
        public void ClipLine_LineCompletelyOutsideIsDropped()
        {
            Assert.Empty(MapBuilder.ClipLine(new List<(double X, double Y)> { (20, 20), (30, 30) }, 0, 10));
        }

        [Fact]
        public void TilesAround_CoverTheRequestedSquare()
        {
            var p = new LocalProjection(52.09, 5.12);
            var tiles = MapBuilder.TilesAround(p, 60, 10);
            Assert.InRange(tiles.Count, 25, 64);
            Assert.Contains((526, 337), tiles);
        }

        [Fact]
        public void ZoomFor_LessDetailForLargeAreas()
        {
            Assert.Equal(10, MapBuilder.ZoomFor(40));
            Assert.Equal(9, MapBuilder.ZoomFor(80));
        }

        [Fact]
        public void AddTile_SortsFeaturesIntoMapKindsAndKeepsPlacesOnce()
        {
            var p = new LocalProjection(52.09, 5.12);
            var tags = (string cls) => (IReadOnlyDictionary<string, string>)new Dictionary<string, string> { ["class"] = cls };
            var line = new List<IReadOnlyList<(double X, double Y)>> { new[] { (0.0, 0.0), (4096.0, 4096.0) } };
            var point = new List<IReadOnlyList<(double X, double Y)>> { new[] { (2000.0, 2000.0) } };
            var outsidePoint = new List<IReadOnlyList<(double X, double Y)>> { new[] { (-30.0, 2000.0) } };
            var features = new List<VectorTile.Feature>
            {
                new("transportation", tags("motorway"), VectorTile.GeometryType.Line, line, 4096),
                new("transportation", tags("service"), VectorTile.GeometryType.Line, line, 4096),
                new("transportation", tags("secondary"), VectorTile.GeometryType.Line, line, 4096),
                new("aeroway", tags("taxiway"), VectorTile.GeometryType.Line, line, 4096),
                new("aeroway", tags("aerodrome"), VectorTile.GeometryType.Polygon, line, 4096),
                new("waterway", tags("river"), VectorTile.GeometryType.Line, line, 4096),
                new("place", new Dictionary<string, string> { ["class"] = "city", ["name"] = "Utrecht" }, VectorTile.GeometryType.Point, point, 4096),
                new("place", new Dictionary<string, string> { ["class"] = "city", ["name"] = "Elders" }, VectorTile.GeometryType.Point, outsidePoint, 4096),
            };

            var content = new MapContent();
            MapBuilder.AddTile(content, features, 10, 526, 337, p);
            MapBuilder.AddTile(content, features.Where(f => f.Layer == "place"), 10, 526, 337, p);

            Assert.Single(content.Shapes[MapKind.Motorway]);
            Assert.Single(content.Shapes[MapKind.River]); // rivers and canals as a thin line
            Assert.Single(content.Shapes[MapKind.Minor]); // secondary roads, not service roads
            Assert.Single(content.Shapes[MapKind.Taxiway]);
            Assert.Single(content.Shapes[MapKind.AirportArea]); // the edge of the grounds, as a line
            var place = Assert.Single(content.Places);
            Assert.Equal("Utrecht", place.Name);
            Assert.Equal(PlaceKind.City, place.Kind);
        }

        [Fact]
        public void AirportLabel_GoesBesideItsGrounds()
        {
            var content = new MapContent();
            content.Add(MapKind.Runway, new[] { (-2.0, 1.0), (3.0, -1.5) });   // a runway through the airport point
            content.Add(MapKind.Runway, new[] { (40.0, 0.0), (45.0, 0.0) });   // another airport far away: ignored
            var placed = MapGeometry.WithLabelBesideGrounds(new MapAirport("AMS", "Schiphol", 0, 0), content);
            Assert.Equal(3.0, placed.LabelX);   // right edge of its runways
            Assert.Equal(-0.25, placed.LabelY); // halfway between the top (-1.5) and bottom (1.0) edge
            var lonely = MapGeometry.WithLabelBesideGrounds(new MapAirport("XXX", "Nergens", 100, 100), content);
            Assert.Equal((100.0, 100.0), (lonely.LabelX, lonely.LabelY));
        }

        [Fact]
        public void AddTile_KeepsAirportsWithACodeOnce()
        {
            var p = new LocalProjection(52.09, 5.12);
            var point = new List<IReadOnlyList<(double X, double Y)>> { new[] { (2000.0, 2000.0) } };
            var features = new List<VectorTile.Feature>
            {
                new("aerodrome_label", new Dictionary<string, string> { ["class"] = "international", ["iata"] = "AMS", ["icao"] = "EHAM", ["name"] = "Luchthaven Schiphol" }, VectorTile.GeometryType.Point, point, 4096),
                new("aerodrome_label", new Dictionary<string, string> { ["class"] = "military", ["icao"] = "EHDL", ["name"] = "Vliegbasis Deelen" }, VectorTile.GeometryType.Point, point, 4096),
                new("aerodrome_label", new Dictionary<string, string> { ["class"] = "private", ["icao"] = "EHXX", ["name"] = "Strip" }, VectorTile.GeometryType.Point, point, 4096),
                new("aerodrome_label", new Dictionary<string, string> { ["class"] = "public", ["name"] = "Zweefveld" }, VectorTile.GeometryType.Point, point, 4096),
            };

            var content = new MapContent();
            MapBuilder.AddTile(content, features, 10, 526, 337, p);
            MapBuilder.AddTile(content, features, 10, 526, 337, p);

            Assert.Equal(new[] { "AMS", "EHDL" }, content.Airports.Select(a => a.Code));
            Assert.Equal("Schiphol", content.Airports[0].Name);
            Assert.Equal("Deelen", content.Airports[1].Name);
        }

        [Theory]
        [InlineData("Luchthaven Schiphol", "Schiphol")]
        [InlineData("Rotterdam The Hague Airport", "Rotterdam The Hague")]
        [InlineData("Eindhoven Airport", "Eindhoven")]
        [InlineData("Brussels International Airport", "Brussels")]
        [InlineData("Lelystad", "Lelystad")]
        public void ShortAirportName_DropsTheWordAirport(string name, string expected) =>
            Assert.Equal(expected, MapBuilder.ShortAirportName(name));

        [Fact]
        public void RunwayTilesFor_CoverTheAirport()
        {
            var p = new LocalProjection(52.0, 4.2);
            var (x, y) = p.ToKm(52.3105, 4.7683); // Schiphol
            var tiles = MapBuilder.RunwayTilesFor(new MapAirport("AMS", "Schiphol", x, y), p);
            Assert.InRange(tiles.Count, 1, 4);
            Assert.Contains(TileMath.TileAt(52.3105, 4.7683, MapBuilder.RunwayZoom), tiles);
        }
    }

    public class FlightCameraTests
    {
        [Fact]
        public void SameMomentSamePhases_SameView()
        {
            var phases = FlightCamera.RandomPhases(new Random(7));
            Assert.Equal(FlightCamera.At(1_800_000_000, phases), FlightCamera.At(1_800_000_000, phases));
        }

        [Fact]
        public void DifferentStart_DifferentView()
        {
            var a = FlightCamera.At(1_800_000_000, FlightCamera.RandomPhases(new Random(1)));
            var b = FlightCamera.At(1_800_000_000, FlightCamera.RandomPhases(new Random(2)));
            Assert.NotEqual(a, b);
        }

        [Fact]
        public void OverADay_StaysInRangeAndMovesSlowlyButSurely()
        {
            var phases = FlightCamera.RandomPhases(new Random(3));
            double minDx = 1, maxDx = -1, minDy = 1, maxDy = -1, maxStep = 0;
            var previous = FlightCamera.At(0, phases);
            for (int s = 1; s <= 24 * 3600; s++)
            {
                var c = FlightCamera.At(s, phases);
                Assert.InRange(c.Dx, -0.42, 0.42);
                Assert.InRange(c.Dy, -0.55, 0.55);
                Assert.InRange(c.RotationDeg, -12, 12);
                Assert.InRange(c.Zoom, 0.88, 1.12);
                maxStep = Math.Max(maxStep, Math.Abs(c.Dx - previous.Dx) + Math.Abs(c.Dy - previous.Dy));
                minDx = Math.Min(minDx, c.Dx); maxDx = Math.Max(maxDx, c.Dx);
                minDy = Math.Min(minDy, c.Dy); maxDy = Math.Max(maxDy, c.Dy);
                previous = c;
            }

            // Slow: well under 1 % of the half view per second.
            Assert.True(maxStep < 0.01, $"step {maxStep}");
            // But it does wander across a large part of its range during a day.
            Assert.True(maxDx - minDx > 0.5 && maxDy - minDy > 0.7, $"dx {minDx:F2}..{maxDx:F2}, dy {minDy:F2}..{maxDy:F2}");
        }
    }

    public class AircraftTests
    {
        private const string Sample = """
        {"now": 1789000000000, "ac": [
          {"hex":"484c24","flight":"KLM44D  ","lat":52.43,"lon":4.35,"track":90,"gs":360,"alt_baro":6150,"seen_pos":2.0},
          {"hex":"aaaaaa","r":"PH-ABC","lat":52.31,"lon":4.76,"alt_baro":"ground","gs":12,"track":180},
          {"hex":"bbbbbb","flight":"OLD1","lat":52.0,"lon":5.0,"seen_pos":120},
          {"hex":"cccccc","flight":"NOPOS"}
        ]}
        """;

        [Fact]
        public void Parse_ReadsPlanesAndSkipsUselessOnes()
        {
            var now = new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);
            var list = AircraftParser.Parse(Sample, now);

            Assert.Equal(2, list.Count);
            var klm = list[0];
            Assert.Equal("KLM44D", klm.Callsign);
            Assert.Equal(6150, klm.AltitudeFeet);
            Assert.False(klm.OnGround);
            Assert.Equal(now.AddSeconds(-2), klm.PositionTimeUtc);

            var ground = list[1];
            Assert.True(ground.OnGround);
            Assert.Equal("PH-ABC", ground.Callsign); // registration when there is no flight number
        }

        [Fact]
        public void Tracker_ExtrapolatesWithSpeedAndHeading()
        {
            var projection = new LocalProjection(52.0, 5.0);
            var tracker = new AircraftTracker(projection);
            var t0 = new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);
            // 360 knots due east = 0.1852 km/s.
            tracker.Update(new[] { new AircraftReport("a", "TEST1", 52.0, 5.0, 90, 360, 30000, false, t0) }, t0);

            var plane = Assert.Single(tracker.Snapshot(t0.AddSeconds(10)));
            Assert.Equal(1.852, plane.X, 3);
            Assert.Equal(0, plane.Y, 3);
        }

        [Fact]
        public void Tracker_NewReportIsBlendedInWithoutAJump()
        {
            var projection = new LocalProjection(52.0, 5.0);
            var tracker = new AircraftTracker(projection);
            var t0 = new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);
            tracker.Update(new[] { new AircraftReport("a", "T", 52.0, 5.0, 90, 360, 30000, false, t0) }, t0);
            double before = tracker.Snapshot(t0.AddSeconds(5)).Single().X;

            // The new report puts the plane 1 km further north.
            var (lat, lon) = projection.ToLatLon(before, -1);
            tracker.Update(new[] { new AircraftReport("a", "T", lat, lon, 90, 360, 30000, false, t0.AddSeconds(5)) }, t0.AddSeconds(5));

            var right = tracker.Snapshot(t0.AddSeconds(5)).Single();
            Assert.Equal(0, right.Y, 3);             // still where it was a moment ago
            var later = tracker.Snapshot(t0.AddSeconds(9)).Single();
            Assert.Equal(-1, later.Y, 2);            // and a few seconds later on the new track
        }

        [Fact]
        public void Tracker_ForgetsPlanesThatStopReporting()
        {
            var tracker = new AircraftTracker(new LocalProjection(52, 5));
            var t0 = new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);
            tracker.Update(new[] { new AircraftReport("a", "T", 52, 5, 0, 300, 20000, false, t0) }, t0);
            tracker.Update(Array.Empty<AircraftReport>(), t0.AddMinutes(2));
            Assert.Equal(0, tracker.Count);
        }

        [Fact]
        public void Parser_ReadsTheClimbRate()
        {
            const string json = """
                {"ac":[{"hex":"a1","flight":"KLM1 ","lat":52,"lon":5,"track":90,"gs":160,"alt_baro":2000,"baro_rate":-768,"t":"B744"},
                       {"hex":"a2","flight":"KLM2 ","lat":52,"lon":5,"track":90,"gs":460,"alt_baro":35000,"geom_rate":64}]}
                """;
            var reports = AircraftParser.Parse(json, DateTime.UtcNow);
            Assert.Equal(-768, reports[0].VerticalRateFpm);
            Assert.Equal(64, reports[1].VerticalRateFpm);
        }

        [Fact]
        public void Plane_TrendApproachAndSpecialName()
        {
            var tracker = new AircraftTracker(new LocalProjection(52, 5));
            var t0 = new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);
            tracker.Update(new[]
            {
                new AircraftReport("land", "KLM1", 52, 5, 90, 150, 2000, false, t0, "B744", VerticalRateFpm: -800),
                new AircraftReport("climb", "KLM2", 52, 5, 90, 160, 2000, false, t0, "A320", VerticalRateFpm: 1500),
                new AircraftReport("level", "KLM3", 52, 5, 90, 450, 35000, false, t0, "A388", VerticalRateFpm: 100),
            }, t0);
            var planes = tracker.Snapshot(t0).ToDictionary(p => p.Hex);
            Assert.True(planes["land"].OnApproach);
            Assert.Equal(-1, planes["land"].Trend);
            Assert.False(planes["climb"].OnApproach);     // taking off: no landing lights drawn
            Assert.Equal(1, planes["climb"].Trend);
            Assert.Equal(0, planes["level"].Trend);
            Assert.Equal("Boeing 747", planes["land"].SpecialName);
            Assert.Equal("Airbus A380", planes["level"].SpecialName);
            Assert.Null(planes["climb"].SpecialName);
        }

        [Fact]
        public void Tracker_PositionOfOnePlane_MatchesTheSnapshot()
        {
            var tracker = new AircraftTracker(new LocalProjection(52, 5));
            var t0 = new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);
            tracker.Update(new[] { new AircraftReport("a", "T", 52, 5, 90, 300, 20000, false, t0, "B738") }, t0);
            var plane = tracker.Snapshot(t0.AddSeconds(20)).Single();
            Assert.True(plane.X > 2.5);                       // flew east, about 3 km in 20 s
            Assert.Equal((plane.X, plane.Y), tracker.PositionOf("a", t0.AddSeconds(20)));
            Assert.Null(tracker.PositionOf("b", t0));
            Assert.Equal(AircraftKind.Narrowbody, plane.Kind);
        }
    }

    public class PlaceSearchTests
    {
        [Theory]
        [InlineData("52.09, 5.12", 52.09, 5.12)]
        [InlineData("52,09; 5,12", 52.09, 5.12)]
        [InlineData(" -33.86 151.21 ", -33.86, 151.21)]
        public void ParseCoordinates_AcceptsCommonForms(string text, double lat, double lon)
        {
            var r = PlaceSearch.ParseCoordinates(text);
            Assert.NotNull(r);
            Assert.Equal(lat, r!.Latitude, 6);
            Assert.Equal(lon, r.Longitude, 6);
        }

        [Theory]
        [InlineData("Utrecht")]
        [InlineData("3511 AB")]
        [InlineData("95, 5")]
        public void ParseCoordinates_RejectsOtherText(string text) => Assert.Null(PlaceSearch.ParseCoordinates(text));

        [Fact]
        public void ShortName_KeepsPlaceAndMunicipality() =>
            Assert.Equal("Domplein, Utrecht", PlaceSearch.ShortName("Domplein, Utrecht, Provincie Utrecht, Nederland, 3512 JE, Nederland"));
    }

    public class FlightsSettingsTests
    {
        private sealed class MemoryStore : OledProtect.Storage.Interfaces.IAppDataFileStore
        {
            public readonly Dictionary<string, object> Files = new();
            public T? Read<T>(string fileName) => Files.TryGetValue(fileName, out var v) ? (T)v : default;
            public bool TryWrite<T>(string fileName, T value) { Files[fileName] = value!; return true; }
        }

        [Fact]
        public void Defaults_AreTheMiddleOfTheNetherlands()
        {
            var s = new FlightsSettings(new MemoryStore());
            Assert.Equal(FlightsSettings.DefaultLatitude, s.Latitude);
            Assert.Equal(FlightsSettings.DefaultRadiusKm, s.RadiusKm);
            Assert.True(s.ShowLabels);
        }

        [Fact]
        public void Tour_DefaultsAndClamping()
        {
            var store = new MemoryStore();
            var s = new FlightsSettings(store);
            Assert.Equal(FlightsMode.Home, s.Mode);
            Assert.True(s.FollowFlights);
            Assert.Equal(FlightsSettings.DefaultSceneSeconds, s.SceneSeconds);

            s.SetMode(FlightsMode.Tour);
            s.SetTourCountries("  Spanje  ");
            s.SetSceneSeconds(500);
            s.SetFollowFlights(false);
            Assert.Equal(FlightsMode.Tour, s.Mode);
            Assert.Equal("Spanje", s.TourCountries);
            Assert.Equal(FlightsSettings.MaxSceneSeconds, s.SceneSeconds);
            Assert.False(s.FollowFlights);
            Assert.Equal(FlightsMode.Tour, new FlightsSettings(store).Mode);
        }

        [Fact]
        public void Changes_AreClampedSavedAndAnnounced()
        {
            var store = new MemoryStore();
            var s = new FlightsSettings(store);
            int changes = 0;
            s.Changed += () => changes++;

            s.SetRadius(500);
            s.SetRadius(500); // no change the second time
            s.SetLocation(51.92, 4.48, "Rotterdam");

            Assert.Equal(FlightsSettings.MaxRadiusKm, s.RadiusKm);
            Assert.Equal(2, changes);
            var reloaded = new FlightsSettings(store);
            Assert.Equal("Rotterdam", reloaded.PlaceName);
            Assert.Equal(51.92, reloaded.Latitude);
        }

        [Fact]
        public void BrokenFile_FallsBackToDefaults()
        {
            var store = new MemoryStore();
            store.Files["flights.json"] = new FlightsSettings.Stored { Latitude = double.NaN, RadiusKm = -4 };
            var s = new FlightsSettings(store);
            Assert.Equal(FlightsSettings.DefaultLatitude, s.Latitude);
            Assert.Equal(FlightsSettings.MinRadiusKm, s.RadiusKm);
        }
    }
}

namespace OledProtect.Tests.Features.Flights
{
    public class SimplifyTests
    {
        [Fact]
        public void Simplify_DropsPointsOnAStraightLineButKeepsCorners()
        {
            var line = new (double X, double Y)[] { (0, 0), (1, 0.001), (2, 0), (3, 0.001), (4, 0), (4, 3) };
            var simple = OledProtect.Features.Flights.MapBuilder.Simplify(line, 0.01);
            Assert.Equal(new[] { (0.0, 0.0), (4.0, 0.0), (4.0, 3.0) }, simple);
        }

        [Fact]
        public void LargerThan_ChecksTheBoundingBox()
        {
            var pond = new (double X, double Y)[] { (0, 0), (0.01, 0), (0.01, 0.01), (0, 0) };
            Assert.False(OledProtect.Features.Flights.MapBuilder.LargerThan(pond, 0.05));
            Assert.True(OledProtect.Features.Flights.MapBuilder.LargerThan(pond, 0.005));
        }
    }
}

namespace OledProtect.Tests.Features.Flights
{
    public class AircraftKindTests
    {
        [Theory]
        [InlineData("B738", "A3", OledProtect.Features.Flights.AircraftKind.Narrowbody)]
        [InlineData("A20N", "", OledProtect.Features.Flights.AircraftKind.Narrowbody)]
        [InlineData("A319", "", OledProtect.Features.Flights.AircraftKind.Narrowbody)]
        [InlineData("A310", "", OledProtect.Features.Flights.AircraftKind.Widebody)]
        [InlineData("B77W", "A5", OledProtect.Features.Flights.AircraftKind.Widebody)]
        [InlineData("B789", "", OledProtect.Features.Flights.AircraftKind.Widebody)]
        [InlineData("A388", "A5", OledProtect.Features.Flights.AircraftKind.Jumbo)]
        [InlineData("B744", "", OledProtect.Features.Flights.AircraftKind.Jumbo)]
        [InlineData("E190", "A3", OledProtect.Features.Flights.AircraftKind.Regional)]
        [InlineData("AT76", "A2", OledProtect.Features.Flights.AircraftKind.Regional)]
        [InlineData("DH8D", "", OledProtect.Features.Flights.AircraftKind.Regional)]
        [InlineData("C172", "A1", OledProtect.Features.Flights.AircraftKind.Light)]
        [InlineData("PA28", "", OledProtect.Features.Flights.AircraftKind.Light)]
        [InlineData("EC35", "", OledProtect.Features.Flights.AircraftKind.Helicopter)]
        [InlineData("", "A7", OledProtect.Features.Flights.AircraftKind.Helicopter)]
        [InlineData("H25B", "A2", OledProtect.Features.Flights.AircraftKind.Regional)]
        [InlineData("", "A5", OledProtect.Features.Flights.AircraftKind.Widebody)]
        [InlineData("", "", OledProtect.Features.Flights.AircraftKind.Narrowbody)]
        public void Classify_ByTypeCodeThenCategory(string type, string category, OledProtect.Features.Flights.AircraftKind expected) =>
            Assert.Equal(expected, OledProtect.Features.Flights.AircraftKinds.Classify(type, category));

        [Fact]
        public void Parser_ReadsTypeAndCategory()
        {
            var list = OledProtect.Features.Flights.AircraftParser.Parse(
                """{"ac":[{"hex":"1","flight":"KLM1","lat":52,"lon":5,"t":"B77W","category":"A5"}]}""", DateTime.UtcNow);
            Assert.Equal("B77W", list[0].TypeCode);
            Assert.Equal(OledProtect.Features.Flights.AircraftKind.Widebody, list[0].Kind);
        }
    }
}

namespace OledProtect.Tests.Features.Flights
{
    public class HeadingTests
    {
        [Fact]
        public void Tracker_KeepsHeadingWhenAlmostStandingStill()
        {
            var tracker = new OledProtect.Features.Flights.AircraftTracker(new OledProtect.Features.Flights.LocalProjection(52, 5));
            var t0 = new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);
            tracker.Update(new[] { new OledProtect.Features.Flights.AircraftReport("h", "LIFE1", 52, 5, 90, 80, 1500, false, t0, "EC35") }, t0);
            // Hovering: the reported track jumps around, the symbol should not.
            tracker.Update(new[] { new OledProtect.Features.Flights.AircraftReport("h", "LIFE1", 52, 5, 270, 3, 1500, false, t0.AddSeconds(5), "EC35") }, t0.AddSeconds(5));
            Assert.Equal(90, tracker.Snapshot(t0.AddSeconds(5)).Single().HeadingDeg);
            // Flying off again: follows the track.
            tracker.Update(new[] { new OledProtect.Features.Flights.AircraftReport("h", "LIFE1", 52, 5, 180, 90, 1500, false, t0.AddSeconds(10), "EC35") }, t0.AddSeconds(10));
            Assert.Equal(180, tracker.Snapshot(t0.AddSeconds(10)).Single().HeadingDeg);
        }
    }
}

namespace OledProtect.Tests.Features.Flights
{
    public class FeedSourcesTests
    {
        private static readonly OledProtect.Features.Flights.FeedSources.Source Fast = new("fast", "https://a/{lat}/{lon}/{nm}");
        private static readonly OledProtect.Features.Flights.FeedSources.Source Reserve = new("reserve", "https://b/{lat}/{lon}/{nm}");
        private static readonly DateTime T0 = new(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void PrefersTheFirstSource()
        {
            var s = new OledProtect.Features.Flights.FeedSources(new[] { Fast, Reserve });
            Assert.Equal(new[] { Fast, Reserve }, s.Available(T0));
        }

        [Fact]
        public void TooManyRequests_RestsThatSourceAMinuteAndUsesTheReserve()
        {
            var s = new OledProtect.Features.Flights.FeedSources(new[] { Fast, Reserve });
            s.Refused(Fast, T0, tooManyRequests: true);
            Assert.Equal(new[] { Reserve }, s.Available(T0.AddSeconds(30)));
            Assert.Equal(new[] { Fast, Reserve }, s.Available(T0.AddSeconds(61)));
        }

        [Fact]
        public void RetryAfter_IsRespectedWithinLimits()
        {
            var s = new OledProtect.Features.Flights.FeedSources(new[] { Fast, Reserve });
            s.Refused(Fast, T0, tooManyRequests: true, TimeSpan.FromHours(5));
            Assert.DoesNotContain(Fast, s.Available(T0.AddMinutes(9)));
            Assert.Contains(Fast, s.Available(T0.AddMinutes(11))); // never longer than 10 minutes
        }

        [Fact]
        public void Url_FillsInThePlaceWithDots()
        {
            Assert.Equal("https://a/52.09/5.12/35", OledProtect.Features.Flights.FeedSources.Url(Fast, 52.09071, 5.12139, 34.6)); // ~1 km, not the exact house
        }
    }
}

namespace OledProtect.Tests.Features.Flights
{
    public class RouteParserTests
    {
        [Theory]
        [InlineData("KLM1234", true)]
        [InlineData("EZY29YK", true)]
        [InlineData("TRA6512", true)]
        [InlineData("PHABC", false)]
        [InlineData("N123AB", false)]
        [InlineData("", false)]
        public void IsAirlineCallsign(string callsign, bool expected) =>
            Assert.Equal(expected, RouteParser.IsAirlineCallsign(callsign));

        [Fact]
        public void Parse_ReadsOriginAndDestination()
        {
            const string json = """
                {"response":{"flightroute":{"callsign":"TRA6512",
                  "origin":{"iata_code":"RTM","icao_code":"EHRD"},
                  "destination":{"iata_code":"","icao_code":"GCTS"}}}}
                """;
            var route = RouteParser.Parse(json);
            Assert.Equal(new FlightRoute("RTM", "GCTS"), route);
            Assert.Equal("RTM→GCTS", route!.ToString());
        }

        [Fact]
        public void Parse_UnknownCallsign_IsNull()
        {
            Assert.Null(RouteParser.Parse("""{"response":"unknown callsign"}"""));
            Assert.Null(RouteParser.Parse("""{"response":{"flightroute":null}}"""));
        }
    }
}
