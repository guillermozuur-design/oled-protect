namespace OledProtect.Features.Flights
{
    /// <summary>
    /// A minimal reader for Mapbox Vector Tiles (the <c>.pbf</c> tiles OpenFreeMap serves): just enough
    /// protobuf to get each layer's features with their tags and geometry. No dependencies.
    /// </summary>
    public static class VectorTile
    {
        public enum GeometryType { Unknown = 0, Point = 1, Line = 2, Polygon = 3 }

        /// <summary>One feature: its tags and its geometry as lists of tile-coordinate points (0..Extent).</summary>
        public sealed record Feature(string Layer, IReadOnlyDictionary<string, string> Tags, GeometryType Type,
                                     IReadOnlyList<IReadOnlyList<(double X, double Y)>> Parts, int Extent);

        /// <summary>Reads all features of the given layers (all layers when <paramref name="layers"/> is null).</summary>
        public static List<Feature> Decode(byte[] data, ISet<string>? layers = null)
        {
            var result = new List<Feature>();
            int i = 0;
            while (i < data.Length)
            {
                var (field, wire) = ReadKey(data, ref i);
                if (field == 3 && wire == 2)
                {
                    var (start, length) = ReadLengthDelimited(data, ref i);
                    DecodeLayer(data, start, start + length, layers, result);
                }
                else Skip(data, ref i, wire);
            }
            return result;
        }

        private static void DecodeLayer(byte[] d, int pos, int end, ISet<string>? wanted, List<Feature> output)
        {
            string name = "";
            int extent = 4096;
            var keys = new List<string>();
            var values = new List<string>();
            var features = new List<(int Start, int End)>();

            int i = pos;
            while (i < end)
            {
                var (field, wire) = ReadKey(d, ref i);
                switch (field)
                {
                    case 1 when wire == 2:
                        {
                            var (s, l) = ReadLengthDelimited(d, ref i);
                            name = System.Text.Encoding.UTF8.GetString(d, s, l);
                            break;
                        }
                    case 2 when wire == 2:
                        {
                            var (s, l) = ReadLengthDelimited(d, ref i);
                            features.Add((s, s + l));
                            break;
                        }
                    case 3 when wire == 2:
                        {
                            var (s, l) = ReadLengthDelimited(d, ref i);
                            keys.Add(System.Text.Encoding.UTF8.GetString(d, s, l));
                            break;
                        }
                    case 4 when wire == 2:
                        {
                            var (s, l) = ReadLengthDelimited(d, ref i);
                            values.Add(ReadValue(d, s, s + l));
                            break;
                        }
                    case 5 when wire == 0:
                        extent = (int)ReadVarint(d, ref i);
                        break;
                    default:
                        Skip(d, ref i, wire);
                        break;
                }
            }

            if (wanted != null && !wanted.Contains(name)) return;
            foreach (var (s, e) in features) output.Add(DecodeFeature(d, s, e, name, keys, values, extent));
        }

        private static string ReadValue(byte[] d, int pos, int end)
        {
            int i = pos;
            string value = "";
            while (i < end)
            {
                var (field, wire) = ReadKey(d, ref i);
                if (field == 1 && wire == 2)
                {
                    var (s, l) = ReadLengthDelimited(d, ref i);
                    value = System.Text.Encoding.UTF8.GetString(d, s, l);
                }
                else if (wire == 0)
                {
                    ulong v = ReadVarint(d, ref i);
                    value = field switch
                    {
                        6 => ZigZag(v).ToString(System.Globalization.CultureInfo.InvariantCulture),
                        7 => (v != 0).ToString(),
                        _ => ((long)v).ToString(System.Globalization.CultureInfo.InvariantCulture)
                    };
                }
                else if (wire == 5)
                {
                    value = BitConverter.ToSingle(d, i).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    i += 4;
                }
                else if (wire == 1)
                {
                    value = BitConverter.ToDouble(d, i).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    i += 8;
                }
                else Skip(d, ref i, wire);
            }
            return value;
        }

        private static Feature DecodeFeature(byte[] d, int pos, int end, string layer, List<string> keys, List<string> values, int extent)
        {
            var tags = new Dictionary<string, string>();
            var type = GeometryType.Unknown;
            var commands = new List<uint>();

            int i = pos;
            while (i < end)
            {
                var (field, wire) = ReadKey(d, ref i);
                if ((field == 2 || field == 4) && wire == 2)
                {
                    var (s, l) = ReadLengthDelimited(d, ref i);
                    var packed = new List<uint>();
                    int j = s;
                    while (j < s + l) packed.Add((uint)ReadVarint(d, ref j));
                    if (field == 2)
                    {
                        for (int k = 0; k + 1 < packed.Count; k += 2)
                        {
                            if (packed[k] < keys.Count && packed[k + 1] < values.Count)
                                tags[keys[(int)packed[k]]] = values[(int)packed[k + 1]];
                        }
                    }
                    else commands = packed;
                }
                else if (field == 3 && wire == 0) type = (GeometryType)(int)ReadVarint(d, ref i);
                else Skip(d, ref i, wire);
            }

            return new Feature(layer, tags, type, DecodeGeometry(commands, type), extent);
        }

        /// <summary>Turns the command stream into parts: lines, polygon rings (closed) or single points.</summary>
        public static List<IReadOnlyList<(double X, double Y)>> DecodeGeometry(IReadOnlyList<uint> commands, GeometryType type)
        {
            var parts = new List<IReadOnlyList<(double X, double Y)>>();
            List<(double X, double Y)>? current = null;
            long x = 0, y = 0;
            int i = 0;
            while (i < commands.Count)
            {
                uint header = commands[i++];
                uint id = header & 7, count = header >> 3;
                if (id == 7)
                {
                    if (current != null && current.Count > 0) current.Add(current[0]);
                    continue;
                }
                for (uint c = 0; c < count && i + 1 < commands.Count; c++)
                {
                    x += ZigZag(commands[i++]);
                    y += ZigZag(commands[i++]);
                    if (id == 1)
                    {
                        if (type == GeometryType.Point)
                        {
                            parts.Add(new[] { ((double)x, (double)y) });
                            continue;
                        }
                        current = new List<(double X, double Y)> { (x, y) };
                        parts.Add(current);
                    }
                    else if (id == 2) current?.Add((x, y));
                }
            }
            return parts;
        }

        // ---- protobuf basics ----

        private static (int Field, int Wire) ReadKey(byte[] d, ref int i)
        {
            ulong key = ReadVarint(d, ref i);
            return ((int)(key >> 3), (int)(key & 7));
        }

        private static ulong ReadVarint(byte[] d, ref int i)
        {
            ulong result = 0;
            int shift = 0;
            while (i < d.Length)
            {
                byte b = d[i++];
                result |= (ulong)(b & 0x7F) << shift;
                if (b < 0x80) break;
                shift += 7;
                if (shift > 63) break;
            }
            return result;
        }

        private static (int Start, int Length) ReadLengthDelimited(byte[] d, ref int i)
        {
            int length = (int)ReadVarint(d, ref i);
            int start = i;
            i = Math.Min(d.Length, i + length);
            return (start, i - start);
        }

        private static void Skip(byte[] d, ref int i, int wire)
        {
            switch (wire)
            {
                case 0: ReadVarint(d, ref i); break;
                case 1: i += 8; break;
                case 2: ReadLengthDelimited(d, ref i); break;
                case 5: i += 4; break;
                default: i = d.Length; break; // unknown: stop reading
            }
        }

        private static long ZigZag(ulong v) => (long)(v >> 1) ^ -(long)(v & 1);
    }

    /// <summary>Web-Mercator tile numbering (the "slippy map" scheme every web map uses).</summary>
    public static class TileMath
    {
        public static (int X, int Y) TileAt(double lat, double lon, int zoom)
        {
            int n = 1 << zoom;
            double latRad = lat * Math.PI / 180;
            int x = (int)Math.Floor((lon + 180) / 360 * n);
            int y = (int)Math.Floor((1 - Math.Log(Math.Tan(latRad) + 1 / Math.Cos(latRad)) / Math.PI) / 2 * n);
            return (Math.Clamp(x, 0, n - 1), Math.Clamp(y, 0, n - 1));
        }

        /// <summary>Latitude/longitude of a point inside a tile (fx, fy in 0..1 from the tile's top-left corner).</summary>
        public static (double Lat, double Lon) ToLatLon(int zoom, int tileX, int tileY, double fx, double fy)
        {
            double n = 1 << zoom;
            double lon = (tileX + fx) / n * 360 - 180;
            double lat = Math.Atan(Math.Sinh(Math.PI * (1 - 2 * (tileY + fy) / n))) * 180 / Math.PI;
            return (lat, lon);
        }
    }

    /// <summary>
    /// Flat map around a centre point, in kilometres (x east, y south, like the screen). Accurate enough for
    /// a region of a few hundred kilometres, and it keeps north up with true distances in every direction.
    /// </summary>
    public readonly struct LocalProjection
    {
        private const double KmPerDegreeLat = 110.574;
        private const double KmPerDegreeLonAtEquator = 111.320;

        public double CenterLat { get; }
        public double CenterLon { get; }
        private readonly double _kmPerDegreeLon;

        public LocalProjection(double centerLat, double centerLon)
        {
            CenterLat = centerLat;
            CenterLon = centerLon;
            _kmPerDegreeLon = KmPerDegreeLonAtEquator * Math.Cos(centerLat * Math.PI / 180);
        }

        public (double X, double Y) ToKm(double lat, double lon) =>
            ((lon - CenterLon) * _kmPerDegreeLon, (CenterLat - lat) * KmPerDegreeLat);

        public (double Lat, double Lon) ToLatLon(double xKm, double yKm) =>
            (CenterLat - yKm / KmPerDegreeLat, CenterLon + xKm / _kmPerDegreeLon);
    }
}
