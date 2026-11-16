using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;

namespace OLED_Sleeper.Features.Flights
{
    /// <summary>One aircraft as reported by the ADS-B feed.</summary>
    public sealed record AircraftReport(string Hex, string Callsign, double Lat, double Lon, double TrackDeg,
                                        double GroundSpeedKnots, double? AltitudeFeet, bool OnGround,
                                        DateTime PositionTimeUtc, string TypeCode = "", string Category = "")
    {
        public AircraftKind Kind => AircraftKinds.Classify(TypeCode, Category);
    }

    /// <summary>What sort of aircraft it is, for its symbol on the map.</summary>
    public enum AircraftKind { Narrowbody, Widebody, Jumbo, Regional, Light, Helicopter }

    /// <summary>
    /// Sorts aircraft by their ICAO type code (B738, A20N, E190, B77W ...) and, when that is unknown, by the
    /// ADS-B emitter category (A1 light ... A5 heavy, A7 rotorcraft).
    /// </summary>
    public static class AircraftKinds
    {
        private static readonly string[] JumboPrefixes = { "A38", "B74", "A34", "A124", "AN22", "AN25" };
        private static readonly string[] WidePrefixes = { "A30", "A310", "A33", "A35", "B76", "B77", "B78", "MD11", "DC10", "IL96", "K35", "KC1" };
        private static readonly string[] NarrowPrefixes = { "A318", "A319", "A32", "A20N", "A21N", "B37", "B38", "B39", "B73", "B75", "BCS", "C919", "MD8", "MD9", "B71", "E29", "T204" };
        private static readonly string[] RegionalPrefixes = { "E1", "E2", "E5", "E6", "CRJ", "AT4", "AT7", "ATP", "DH8", "F70", "F100", "F50", "SF34", "SB20", "J328", "D328", "RJ", "B46", "AN2", "SW4", "JS3", "BE20", "C208", "PC12", "B190", "DHC6" };
        private static readonly string[] HelicopterPrefixes = { "EC", "H125", "H130", "H135", "H145", "H155", "H160", "H175", "H215", "H225", "AS3", "AS5", "AS6", "AW1", "A109", "A119", "A139", "A169", "A189", "B06", "B407", "B412", "B429", "S76", "S92", "R22", "R44", "R66", "NH90", "EH10", "MI8", "BK17", "CH47", "H60", "UH1", "LYNX", "PUMA", "GAZL" };

        public static AircraftKind Classify(string typeCode, string category)
        {
            string t = (typeCode ?? "").Trim().ToUpperInvariant();
            string c = (category ?? "").Trim().ToUpperInvariant();

            if (c == "A7" || HelicopterPrefixes.Any(p => t.StartsWith(p, StringComparison.Ordinal))) return AircraftKind.Helicopter;
            if (t.Length > 0)
            {
                // Most specific first: "A310" is a widebody, "A318"/"A319" are narrowbodies.
                if (JumboPrefixes.Any(p => t.StartsWith(p, StringComparison.Ordinal))) return AircraftKind.Jumbo;
                if (t == "A310" || (WidePrefixes.Any(p => t.StartsWith(p, StringComparison.Ordinal)) && !t.StartsWith("A318") && !t.StartsWith("A319")))
                    return AircraftKind.Widebody;
                if (NarrowPrefixes.Any(p => t.StartsWith(p, StringComparison.Ordinal))) return AircraftKind.Narrowbody;
                if (RegionalPrefixes.Any(p => t.StartsWith(p, StringComparison.Ordinal))) return AircraftKind.Regional;
            }
            return c switch
            {
                "A1" or "B1" or "B4" => AircraftKind.Light,
                "A2" => AircraftKind.Regional,
                "A5" => AircraftKind.Widebody,
                _ when t.Length > 0 && char.IsLetter(t[0]) && t.Length <= 4 && (t.StartsWith('C') || t.StartsWith('P') || t.StartsWith("DA") || t.StartsWith("SR")) => AircraftKind.Light,
                _ => AircraftKind.Narrowbody
            };
        }
    }

    /// <summary>Reads the JSON of an adsb.lol / readsb style feed (<c>{"now": ms, "ac": [...]}</c>).</summary>
    public static class AircraftParser
    {
        public static List<AircraftReport> Parse(string json, DateTime receivedUtc)
        {
            var result = new List<AircraftReport>();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Times in the feed are relative to the server's clock; anchor them to our own receive time.
            if (!root.TryGetProperty("ac", out var list) || list.ValueKind != JsonValueKind.Array) return result;

            foreach (var a in list.EnumerateArray())
            {
                if (!TryNumber(a, "lat", out double lat) || !TryNumber(a, "lon", out double lon)) continue;
                string hex = a.TryGetProperty("hex", out var h) ? h.GetString() ?? "" : "";
                if (hex.Length == 0) continue;

                string callsign = a.TryGetProperty("flight", out var f) ? (f.GetString() ?? "").Trim() : "";
                if (callsign.Length == 0 && a.TryGetProperty("r", out var r)) callsign = (r.GetString() ?? "").Trim();

                double track = TryNumber(a, "track", out double t) ? t : TryNumber(a, "true_heading", out double th) ? th : 0;
                double gs = TryNumber(a, "gs", out double g) ? g : 0;
                double seenPos = TryNumber(a, "seen_pos", out double s) ? s : 0;
                if (seenPos > 60) continue; // position too old to be useful

                bool onGround = false;
                double? altitude = null;
                if (a.TryGetProperty("alt_baro", out var alt))
                {
                    if (alt.ValueKind == JsonValueKind.String && alt.GetString() == "ground") onGround = true;
                    else if (alt.ValueKind == JsonValueKind.Number) altitude = alt.GetDouble();
                }
                if (altitude == null && TryNumber(a, "alt_geom", out double geom)) altitude = geom;

                string type = a.TryGetProperty("t", out var ty) && ty.ValueKind == JsonValueKind.String ? ty.GetString() ?? "" : "";
                string category = a.TryGetProperty("category", out var ca) && ca.ValueKind == JsonValueKind.String ? ca.GetString() ?? "" : "";
                result.Add(new AircraftReport(hex, callsign, lat, lon, track, gs, altitude, onGround,
                                              receivedUtc - TimeSpan.FromSeconds(seenPos), type, category));
            }
            return result;
        }

        private static bool TryNumber(JsonElement e, string name, out double value)
        {
            value = 0;
            if (!e.TryGetProperty(name, out var p)) return false;
            if (p.ValueKind == JsonValueKind.Number) { value = p.GetDouble(); return true; }
            if (p.ValueKind == JsonValueKind.String)
                return double.TryParse(p.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
            return false;
        }
    }

    /// <summary>
    /// Keeps every aircraft moving smoothly between feed updates: positions are extrapolated with speed and
    /// heading, and when a new report disagrees the difference is blended away over a few seconds instead
    /// of making the plane jump. Also remembers a short trail behind each plane.
    /// </summary>
    public sealed class AircraftTracker
    {
        public const double KnotsToKmPerSecond = 1.852 / 3600;
        private static readonly TimeSpan CorrectionTime = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan ForgetAfter = TimeSpan.FromSeconds(45);
        private static readonly TimeSpan TrailLength = TimeSpan.FromSeconds(50);
        private static readonly TimeSpan TrailStep = TimeSpan.FromSeconds(1);

        /// <summary>A plane's current state, in local kilometres.</summary>
        public sealed record Plane(string Hex, string Callsign, double X, double Y, double HeadingDeg,
                                   double? AltitudeFeet, bool OnGround, IReadOnlyList<(double X, double Y, double AgeSeconds)> Trail,
                                   AircraftKind Kind = AircraftKind.Narrowbody, string TypeCode = "");

        private sealed class Track
        {
            public required AircraftReport Report;
            public double X0, Y0, Vx, Vy;          // position at the report time + velocity, km and km/s
            public double CorrX, CorrY;            // offset still to blend away
            public DateTime CorrectionStart;
            public DateTime LastSeenUtc;
            public readonly List<(double X, double Y, DateTime T)> Trail = new();
        }

        private readonly Dictionary<string, Track> _tracks = new();
        private LocalProjection _projection;

        public AircraftTracker(LocalProjection projection) => _projection = projection;

        public int Count => _tracks.Count;

        /// <summary>A new projection (the pin moved): forget everything.</summary>
        public void Reset(LocalProjection projection)
        {
            _projection = projection;
            _tracks.Clear();
        }

        public void Update(IEnumerable<AircraftReport> reports, DateTime nowUtc)
        {
            foreach (var r in reports)
            {
                var (x, y) = _projection.ToKm(r.Lat, r.Lon);
                double speed = r.OnGround ? 0 : r.GroundSpeedKnots * KnotsToKmPerSecond;
                double rad = r.TrackDeg * Math.PI / 180;
                double vx = Math.Sin(rad) * speed, vy = -Math.Cos(rad) * speed; // y points south

                if (_tracks.TryGetValue(r.Hex, out var t))
                {
                    var (oldX, oldY) = Position(t, nowUtc);
                    t.Report = r;
                    t.X0 = x; t.Y0 = y; t.Vx = vx; t.Vy = vy;
                    var (newX, newY) = Raw(t, nowUtc);
                    t.CorrX = oldX - newX;
                    t.CorrY = oldY - newY;
                    // A big disagreement (a plane that was out of range for a while): just jump.
                    if (Math.Abs(t.CorrX) + Math.Abs(t.CorrY) > 5) t.CorrX = t.CorrY = 0;
                    t.CorrectionStart = nowUtc;
                    t.LastSeenUtc = nowUtc;
                }
                else
                {
                    _tracks[r.Hex] = new Track { Report = r, X0 = x, Y0 = y, Vx = vx, Vy = vy, LastSeenUtc = nowUtc, CorrectionStart = nowUtc };
                }
            }

            foreach (var gone in _tracks.Where(kv => nowUtc - kv.Value.LastSeenUtc > ForgetAfter).Select(kv => kv.Key).ToList())
                _tracks.Remove(gone);
        }

        /// <summary>All planes at this moment (also grows their trails).</summary>
        public List<Plane> Snapshot(DateTime nowUtc)
        {
            var list = new List<Plane>(_tracks.Count);
            foreach (var t in _tracks.Values)
            {
                var (x, y) = Position(t, nowUtc);
                if (t.Trail.Count == 0 || nowUtc - t.Trail[^1].T >= TrailStep) t.Trail.Add((x, y, nowUtc));
                while (t.Trail.Count > 0 && nowUtc - t.Trail[0].T > TrailLength) t.Trail.RemoveAt(0);

                var trail = t.Trail.Select(p => (p.X, p.Y, (nowUtc - p.T).TotalSeconds)).ToList();
                trail.Add((x, y, 0));
                list.Add(new Plane(t.Report.Hex, t.Report.Callsign, x, y, t.Report.TrackDeg, t.Report.AltitudeFeet, t.Report.OnGround, trail,
                                   t.Report.Kind, t.Report.TypeCode));
            }
            return list;
        }

        private static (double X, double Y) Raw(Track t, DateTime nowUtc)
        {
            // Extrapolate at most a minute ahead (a plane that stops reporting should not fly off forever).
            double dt = Math.Clamp((nowUtc - t.Report.PositionTimeUtc).TotalSeconds, 0, 60);
            return (t.X0 + t.Vx * dt, t.Y0 + t.Vy * dt);
        }

        private static (double X, double Y) Position(Track t, DateTime nowUtc)
        {
            var (x, y) = Raw(t, nowUtc);
            double k = 1 - Math.Clamp((nowUtc - t.CorrectionStart).TotalSeconds / CorrectionTime.TotalSeconds, 0, 1);
            k = k * k * (3 - 2 * k); // smooth
            return (x + t.CorrX * k, y + t.CorrY * k);
        }
    }

    /// <summary>
    /// Fetches the aircraft around the pin every few seconds from adsb.lol (free, open ADS-B data), but only
    /// while a flights screensaver is on screen. Shared by all screens, so two screens make one request.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public static class AircraftFeed
    {
        private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);
        private static readonly HttpClient Http = CreateClient();
        private static readonly object Gate = new();
        private static readonly List<Subscription> Subscribers = new();
        private static System.Threading.Timer? _timer;
        private static bool _busy;

        /// <summary>The latest reports and when they arrived (null until the first answer).</summary>
        public static (List<AircraftReport> Reports, DateTime ReceivedUtc)? Latest { get; private set; }

        /// <summary>True when the last request failed (no internet, service down).</summary>
        public static bool LastRequestFailed { get; private set; }

        /// <summary>Raised (on a background thread) after every successful update.</summary>
        public static event Action? Updated;

        private sealed class Subscription : IDisposable
        {
            public double Lat, Lon, RadiusNm;
            public void Dispose()
            {
                lock (Gate)
                {
                    Subscribers.Remove(this);
                    if (Subscribers.Count == 0)
                    {
                        _timer?.Dispose();
                        _timer = null;
                    }
                }
            }
        }

        public static IDisposable Subscribe(double lat, double lon, double radiusKm)
        {
            var s = new Subscription { Lat = lat, Lon = lon, RadiusNm = Math.Clamp(Math.Ceiling(radiusKm / 1.852), 5, 250) };
            lock (Gate)
            {
                // A different place than what we have: the old planes are not wanted any more.
                if (Subscribers.Count == 0 || Subscribers[^1].Lat != lat || Subscribers[^1].Lon != lon) Latest = null;
                Subscribers.Add(s);
                _timer ??= new System.Threading.Timer(_ => _ = PollAsync(), null, TimeSpan.Zero, Interval);
            }
            return s;
        }

        private static async Task PollAsync()
        {
            Subscription? s;
            lock (Gate)
            {
                if (_busy || Subscribers.Count == 0) return;
                _busy = true;
                s = Subscribers[^1];
            }

            try
            {
                string url = string.Create(CultureInfo.InvariantCulture,
                    $"https://api.adsb.lol/v2/point/{s.Lat:0.####}/{s.Lon:0.####}/{s.RadiusNm:0}");
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                string json = await Http.GetStringAsync(url, cts.Token);
                var reports = AircraftParser.Parse(json, DateTime.UtcNow);
                Latest = (reports, DateTime.UtcNow);
                LastRequestFailed = false;
                Updated?.Invoke();
            }
            catch (Exception ex)
            {
                if (!LastRequestFailed) Serilog.Log.Warning("Aircraft feed unavailable: {Message}", ex.Message);
                LastRequestFailed = true;
            }
            finally
            {
                lock (Gate) _busy = false;
            }
        }

        internal static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("OLED-Space-Sleeper (+https://github.com/guillermozuur-design/oled-space-sleeper)");
            return client;
        }
    }
}
