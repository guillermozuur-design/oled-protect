using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace OLED_Sleeper.Features.Flights
{
    /// <summary>What a line or area on the map is.</summary>
    public enum MapKind { Water, River, Border, Minor, Major, Motorway, Rail, Runway, RunwayArea }

    public enum PlaceKind { City, Town, Village }

    public sealed record MapPlace(string Name, PlaceKind Kind, double X, double Y);

    /// <summary>The map around the pin: lines and areas per kind, and place names, all in local kilometres.</summary>
    public sealed class MapContent
    {
        public Dictionary<MapKind, List<(double X, double Y)[]>> Shapes { get; } = new();
        public List<MapPlace> Places { get; } = new();

        public void Add(MapKind kind, (double X, double Y)[] points)
        {
            if (points.Length < 2) return;
            if (!Shapes.TryGetValue(kind, out var list)) Shapes[kind] = list = new();
            list.Add(points);
        }

        public int ShapeCount => Shapes.Values.Sum(l => l.Count);
    }

    /// <summary>
    /// Turns the features of one vector tile into map shapes. Only what the screensaver draws is kept: big
    /// roads, railways, rivers and water, runways, country borders and the names of towns and cities.
    /// </summary>
    public static class MapBuilder
    {
        public static readonly HashSet<string> Layers = new() { "transportation", "water", "waterway", "aeroway", "boundary", "place" };

        /// <summary>Lines are cut at the tile edge (plus a hair, so neighbours meet without a gap).</summary>
        private const double EdgeMargin = 2;

        public static void AddTile(MapContent content, IEnumerable<VectorTile.Feature> features, int zoom, int tileX, int tileY, LocalProjection projection)
        {
            foreach (var f in features)
            {
                f.Tags.TryGetValue("class", out var cls);
                switch (f.Layer)
                {
                    case "transportation" when f.Type == VectorTile.GeometryType.Line:
                        {
                            MapKind? kind = cls switch
                            {
                                "motorway" => MapKind.Motorway,
                                "trunk" or "primary" => MapKind.Major,
                                "secondary" or "tertiary" => MapKind.Minor,
                                "rail" when !f.Tags.ContainsKey("service") => MapKind.Rail,
                                _ => null
                            };
                            if (kind != null) AddLines(content, kind.Value, f, zoom, tileX, tileY, projection);
                            break;
                        }
                    case "waterway" when f.Type == VectorTile.GeometryType.Line && (cls == "river" || cls == "canal"):
                        AddLines(content, MapKind.River, f, zoom, tileX, tileY, projection);
                        break;
                    case "water" when f.Type == VectorTile.GeometryType.Polygon && cls != "swimming_pool":
                        // Water as an outline only (coast, banks, lake shores): black inside, kind to OLED. Cutting
                        // the rings like lines also removes the artificial edges where a tile cut the polygon.
                        AddLines(content, MapKind.Water, f, zoom, tileX, tileY, projection);
                        break;
                    case "aeroway" when cls == "runway":
                        if (f.Type == VectorTile.GeometryType.Line) AddLines(content, MapKind.Runway, f, zoom, tileX, tileY, projection);
                        else if (f.Type == VectorTile.GeometryType.Polygon) AddAreas(content, MapKind.RunwayArea, f, zoom, tileX, tileY, projection);
                        break;
                    case "boundary" when f.Type == VectorTile.GeometryType.Line
                                         && f.Tags.TryGetValue("admin_level", out var level) && level == "2"
                                         && !(f.Tags.TryGetValue("maritime", out var m) && m is "1" or "True"):
                        AddLines(content, MapKind.Border, f, zoom, tileX, tileY, projection);
                        break;
                    case "place" when f.Type == VectorTile.GeometryType.Point:
                        AddPlace(content, f, cls, zoom, tileX, tileY, projection);
                        break;
                }
            }
        }

        private static void AddPlace(MapContent content, VectorTile.Feature f, string? cls, int zoom, int tileX, int tileY, LocalProjection projection)
        {
            PlaceKind? kind = cls switch { "city" => PlaceKind.City, "town" => PlaceKind.Town, "village" => PlaceKind.Village, _ => null };
            if (kind == null || f.Parts.Count == 0) return;

            string name = f.Tags.TryGetValue("name:nl", out var nl) && nl.Length > 0 ? nl
                        : f.Tags.TryGetValue("name", out var n) ? n : "";
            if (name.Length == 0) return;

            var (px, py) = f.Parts[0][0];
            // Tiles repeat nearby places in their margins: keep each place once, from the tile that really contains it.
            if (px < 0 || py < 0 || px >= f.Extent || py >= f.Extent) return;
            var (lat, lon) = TileMath.ToLatLon(zoom, tileX, tileY, px / f.Extent, py / f.Extent);
            var (x, y) = projection.ToKm(lat, lon);
            if (content.Places.Any(p => p.Name == name && Math.Abs(p.X - x) < 2 && Math.Abs(p.Y - y) < 2)) return;
            content.Places.Add(new MapPlace(name, kind.Value, x, y));
        }

        private static void AddAreas(MapContent content, MapKind kind, VectorTile.Feature f, int zoom, int tileX, int tileY, LocalProjection projection)
        {
            // Areas are filled, never outlined, so the parts that stick out over the tile edge simply overlap the
            // neighbour's identical fill and are invisible.
            foreach (var ring in f.Parts)
                content.Add(kind, ring.Select(p => ToKm(p, f.Extent, zoom, tileX, tileY, projection)).ToArray());
        }

        private static void AddLines(MapContent content, MapKind kind, VectorTile.Feature f, int zoom, int tileX, int tileY, LocalProjection projection)
        {
            foreach (var part in f.Parts)
            {
                foreach (var piece in ClipLine(part, -EdgeMargin, f.Extent + EdgeMargin))
                    content.Add(kind, piece.Select(p => ToKm(p, f.Extent, zoom, tileX, tileY, projection)).ToArray());
            }
        }

        private static (double X, double Y) ToKm((double X, double Y) p, int extent, int zoom, int tileX, int tileY, LocalProjection projection)
        {
            var (lat, lon) = TileMath.ToLatLon(zoom, tileX, tileY, p.X / extent, p.Y / extent);
            return projection.ToKm(lat, lon);
        }

        /// <summary>Cuts a polyline to the square [min, max]², returning the pieces inside.</summary>
        public static List<List<(double X, double Y)>> ClipLine(IReadOnlyList<(double X, double Y)> line, double min, double max)
        {
            var pieces = new List<List<(double X, double Y)>>();
            List<(double X, double Y)>? current = null;
            for (int i = 1; i < line.Count; i++)
            {
                var a = line[i - 1];
                var b = line[i];
                if (ClipSegment(ref a, ref b, min, max))
                {
                    if (current == null || current[^1] != a)
                    {
                        current = new List<(double X, double Y)> { a };
                        pieces.Add(current);
                    }
                    current.Add(b);
                }
                else current = null;
            }
            return pieces;
        }

        private static bool ClipSegment(ref (double X, double Y) a, ref (double X, double Y) b, double min, double max)
        {
            // Liang–Barsky
            double dx = b.X - a.X, dy = b.Y - a.Y, t0 = 0, t1 = 1;
            double[] p = { -dx, dx, -dy, dy };
            double[] q = { a.X - min, max - a.X, a.Y - min, max - a.Y };
            for (int i = 0; i < 4; i++)
            {
                if (p[i] == 0)
                {
                    if (q[i] < 0) return false;
                    continue;
                }
                double r = q[i] / p[i];
                if (p[i] < 0) { if (r > t1) return false; if (r > t0) t0 = r; }
                else { if (r < t0) return false; if (r < t1) t1 = r; }
            }
            var start = (a.X + t0 * dx, a.Y + t0 * dy);
            var end = (a.X + t1 * dx, a.Y + t1 * dy);
            a = start;
            b = end;
            return true;
        }

        /// <summary>Douglas–Peucker: drops points that change the line by less than the tolerance.</summary>
        public static (double X, double Y)[] Simplify((double X, double Y)[] points, double tolerance)
        {
            if (points.Length <= 2 || tolerance <= 0) return points;
            var keep = new bool[points.Length];
            keep[0] = keep[^1] = true;
            var stack = new Stack<(int, int)>();
            stack.Push((0, points.Length - 1));
            while (stack.Count > 0)
            {
                var (first, last) = stack.Pop();
                double maxDistance = 0;
                int index = -1;
                for (int i = first + 1; i < last; i++)
                {
                    double d = DistanceToSegment(points[i], points[first], points[last]);
                    if (d > maxDistance) { maxDistance = d; index = i; }
                }
                if (index >= 0 && maxDistance > tolerance)
                {
                    keep[index] = true;
                    stack.Push((first, index));
                    stack.Push((index, last));
                }
            }
            var result = new List<(double X, double Y)>(points.Length);
            for (int i = 0; i < points.Length; i++) if (keep[i]) result.Add(points[i]);
            return result.ToArray();
        }

        private static double DistanceToSegment((double X, double Y) p, (double X, double Y) a, (double X, double Y) b)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double lengthSquared = dx * dx + dy * dy;
            double t = lengthSquared == 0 ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lengthSquared, 0, 1);
            double ex = a.X + t * dx - p.X, ey = a.Y + t * dy - p.Y;
            return Math.Sqrt(ex * ex + ey * ey);
        }

        /// <summary>Whether a shape is wider or taller than the given size.</summary>
        public static bool LargerThan((double X, double Y)[] points, double size) =>
            points.Max(p => p.X) - points.Min(p => p.X) > size || points.Max(p => p.Y) - points.Min(p => p.Y) > size;

        /// <summary>Which zoom level to use for a radius: more detail when close, fewer tiles when far.</summary>
        public static int ZoomFor(double radiusKm) => radiusKm <= 45 ? 10 : 9;

        /// <summary>The tiles that cover the square of <paramref name="halfSizeKm"/> around the centre.</summary>
        public static List<(int X, int Y)> TilesAround(LocalProjection projection, double halfSizeKm, int zoom)
        {
            var (north, west) = projection.ToLatLon(-halfSizeKm, -halfSizeKm);
            var (south, east) = projection.ToLatLon(halfSizeKm, halfSizeKm);
            var (x0, y0) = TileMath.TileAt(north, west, zoom);
            var (x1, y1) = TileMath.TileAt(south, east, zoom);
            var tiles = new List<(int X, int Y)>();
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                    tiles.Add((x, y));
            return tiles;
        }
    }

    /// <summary>
    /// The map ready for drawing, cut into square chunks so only the chunks on a screen are drawn. Lines are
    /// simplified to what is visible at screen resolution and tiny ponds are left out, which keeps drawing cheap.
    /// </summary>
    public sealed class MapGeometry
    {
        public sealed record Chunk(MapKind Kind, Rect Bounds, Geometry Geometry);

        /// <summary>Size of a chunk, in km.</summary>
        public const double ChunkKm = 12;

        public required LocalProjection Projection { get; init; }
        public required double RadiusKm { get; init; }
        public required IReadOnlyList<Chunk> Chunks { get; init; }
        public required IReadOnlyList<MapPlace> Places { get; init; }
        public int PointCount { get; init; }

        /// <param name="toleranceKm">Detail that may be dropped (about half a pixel at the normal zoom).</param>
        public static MapGeometry From(MapContent content, LocalProjection projection, double radiusKm, double toleranceKm)
        {
            var chunks = new List<Chunk>();
            int points = 0;
            foreach (var (kind, shapes) in content.Shapes)
            {
                bool filled = kind == MapKind.RunwayArea;
                var cells = new Dictionary<(int, int), List<(double X, double Y)[]>>();
                foreach (var shape in shapes)
                {
                    var simple = MapBuilder.Simplify(shape, toleranceKm);
                    if ((filled || kind == MapKind.Water) && !MapBuilder.LargerThan(simple, toleranceKm * 10)) continue; // ditches, ponds, greenhouse basins: specks
                    if (simple.Length < 2) continue;
                    var (cx, cy) = (simple.Average(p => p.X), simple.Average(p => p.Y));
                    var key = ((int)Math.Floor(cx / ChunkKm), (int)Math.Floor(cy / ChunkKm));
                    if (!cells.TryGetValue(key, out var list)) cells[key] = list = new();
                    list.Add(simple);
                }

                foreach (var list in cells.Values)
                {
                    var g = new StreamGeometry { FillRule = FillRule.Nonzero };
                    double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
                    using (var ctx = g.Open())
                    {
                        foreach (var s in list)
                        {
                            ctx.BeginFigure(new Point(s[0].X, s[0].Y), filled, filled);
                            ctx.PolyLineTo(s.Skip(1).Select(p => new Point(p.X, p.Y)).ToList(), !filled, true);
                            foreach (var (x, y) in s)
                            {
                                minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                                minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                            }
                            points += s.Length;
                        }
                    }
                    g.Freeze();
                    chunks.Add(new Chunk(kind, new Rect(minX, minY, maxX - minX, maxY - minY), g));
                }
            }
            return new MapGeometry { Projection = projection, RadiusKm = radiusKm, Chunks = chunks, Places = content.Places.ToList(), PointCount = points };
        }
    }

    /// <summary>
    /// Gets the map for a place: OpenStreetMap data as vector tiles from OpenFreeMap (free, no account),
    /// kept on disk so every place is downloaded only once. Shared by all screens.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public static class MapLoader
    {
        private const string TileJsonUrl = "https://tiles.openfreemap.org/planet";
        private static readonly TimeSpan CacheLifetime = TimeSpan.FromDays(90);
        private static readonly HttpClient Http = AircraftFeed.CreateClient();
        private static readonly SemaphoreSlim Downloads = new(4);
        private static readonly object Gate = new();
        private static (string Key, Task<MapGeometry?> Task)? _current;

        /// <summary>The map around a place (cached in memory for the last place asked for).</summary>
        public static Task<MapGeometry?> GetAsync(double lat, double lon, double radiusKm, string? cacheDirectory)
        {
            string key = FormattableString.Invariant($"{lat:0.#####},{lon:0.#####},{radiusKm:0.#}");
            lock (Gate)
            {
                if (_current is { } c && c.Key == key && !(c.Task.IsCompleted && c.Task.Result == null)) return c.Task;
                var task = Task.Run(() => LoadAsync(lat, lon, radiusKm, cacheDirectory));
                _current = (key, task);
                return task;
            }
        }

        private static async Task<MapGeometry?> LoadAsync(double lat, double lon, double radiusKm, string? cacheDirectory)
        {
            var projection = new LocalProjection(lat, lon);
            int zoom = MapBuilder.ZoomFor(radiusKm);
            // Enough around the pin for the slow drift, zoom and turn of the map (see FlightCamera).
            var tiles = MapBuilder.TilesAround(projection, radiusKm * 1.7, zoom);

            string? template = null;
            var content = new MapContent();
            int loaded = 0;
            var results = await Task.WhenAll(tiles.Select(async t =>
            {
                string? file = cacheDirectory != null ? Path.Combine(cacheDirectory, zoom.ToString(), t.X.ToString(), t.Y + ".pbf") : null;
                byte[]? data = null;
                try
                {
                    if (file != null && File.Exists(file) && DateTime.UtcNow - File.GetLastWriteTimeUtc(file) < CacheLifetime)
                        data = await File.ReadAllBytesAsync(file);
                    else
                    {
                        template ??= await TileTemplateAsync();
                        await Downloads.WaitAsync();
                        try
                        {
                            data = await Http.GetByteArrayAsync(template.Replace("{z}", zoom.ToString()).Replace("{x}", t.X.ToString()).Replace("{y}", t.Y.ToString()));
                        }
                        finally { Downloads.Release(); }
                        if (data.Length > 2 && data[0] == 0x1F && data[1] == 0x8B) data = Gunzip(data);
                        if (file != null)
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                            await File.WriteAllBytesAsync(file, data);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Serilog.Log.Debug("Map tile {Z}/{X}/{Y} not available: {Message}", zoom, t.X, t.Y, ex.Message);
                    // An old cached copy is better than nothing.
                    if (data == null && file != null && File.Exists(file)) data = await File.ReadAllBytesAsync(file);
                }
                return (t, data);
            }));

            foreach (var (t, data) in results)
            {
                if (data == null) continue;
                try
                {
                    MapBuilder.AddTile(content, VectorTile.Decode(data, MapBuilder.Layers), zoom, t.X, t.Y, projection);
                    loaded++;
                }
                catch (Exception ex)
                {
                    Serilog.Log.Debug("Map tile {Z}/{X}/{Y} could not be read: {Message}", zoom, t.X, t.Y, ex.Message);
                }
            }

            if (loaded == 0) return null;
            // About one pixel at the normal zoom on two 2560-pixel-wide screens; finer detail cannot be seen anyway.
            var map = MapGeometry.From(content, projection, radiusKm, radiusKm / 2560);
            Serilog.Log.Information("Flight map: {Loaded}/{Total} tiles, {Shapes} shapes ({Points} points after simplifying, {Chunks} chunks), {Places} places.",
                loaded, tiles.Count, content.ShapeCount, map.PointCount, map.Chunks.Count, content.Places.Count);
            return map;
        }

        private static byte[] Gunzip(byte[] data)
        {
            using var input = new MemoryStream(data);
            using var gzip = new System.IO.Compression.GZipStream(input, System.IO.Compression.CompressionMode.Decompress);
            using var output = new MemoryStream();
            gzip.CopyTo(output);
            return output.ToArray();
        }

        private static string? _template;

        private static async Task<string> TileTemplateAsync()
        {
            if (_template != null) return _template;
            // The tile address contains the date of the map build; the TileJSON says which one is current.
            using var doc = JsonDocument.Parse(await Http.GetStringAsync(TileJsonUrl));
            _template = doc.RootElement.GetProperty("tiles")[0].GetString()!;
            return _template;
        }
    }
}
