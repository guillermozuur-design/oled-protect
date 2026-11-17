using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace OLED_Sleeper.Features.Flights
{
    /// <summary>An airport the tour can visit.</summary>
    public sealed record TourAirport(string Code, string Country, double Lat, double Lon, double Weight, string Name);

    /// <summary>
    /// The airports with scheduled flights, built into the app (from OurAirports, public domain). Big airports
    /// weigh more than medium ones, and the busiest hubs in the world weigh most, so they come by more often.
    /// </summary>
    public static class AirportCatalog
    {
        /// <summary>The busiest airports (passengers per year), shown most often.</summary>
        private static readonly HashSet<string> Hubs = new(StringComparer.Ordinal)
        {
            "AMS", "LHR", "CDG", "FRA", "IST", "MAD", "BCN", "MUC", "FCO", "LGW", "DUB", "ZRH", "CPH", "PMI", "VIE",
            "OSL", "ARN", "BRU", "LIS", "MAN", "DUS", "HAM", "ATH", "WAW", "BER", "MXP", "HEL", "PRG", "BUD", "AGP",
            "ALC", "ORY", "NCE", "GVA", "STN", "TFS", "LPA", "SAW", "AYT", "OPO", "ATL", "DFW", "DEN", "ORD", "LAX",
            "JFK", "DXB", "DOH", "HND", "NRT", "ICN", "SIN", "HKG", "PEK", "PVG", "CAN", "BKK", "DEL", "BOM", "SYD",
            "YYZ", "GRU", "MEX", "LAS", "MCO", "MIA", "SFO", "SEA", "CLT", "PHX", "EWR", "IAH", "KUL", "CGK", "TPE",
            "JED", "RUH", "CAI", "JNB"
        };

        public const double HubWeight = 8;

        private static readonly Lazy<IReadOnlyList<TourAirport>> Embedded = new(() => Parse(ReadResource("airports.txt")));

        public static IReadOnlyList<TourAirport> All => Embedded.Value;

        /// <summary>Lines "IATA|country|lat|lon|weight|name"; lines starting with # are notes.</summary>
        public static List<TourAirport> Parse(string text)
        {
            var list = new List<TourAirport>();
            foreach (var line in text.Split('\n'))
            {
                if (line.Length == 0 || line[0] == '#') continue;
                var f = line.TrimEnd('\r').Split('|');
                if (f.Length < 6) continue;
                if (!double.TryParse(f[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) ||
                    !double.TryParse(f[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon) ||
                    !double.TryParse(f[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var weight)) continue;
                if (Hubs.Contains(f[0])) weight = HubWeight;
                list.Add(new TourAirport(f[0], f[1], lat, lon, weight, f[5]));
            }
            return list;
        }

        internal static string ReadResource(string fileName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var name = assembly.GetManifestResourceNames().First(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }

    public sealed record Country(string Code, string Name);

    /// <summary>Recognises the countries typed in the settings ("Nederland, Spanje, UK, Deutschland").</summary>
    public static class Countries
    {
        private static readonly Lazy<(List<Country> List, Dictionary<string, Country> Lookup)> Embedded =
            new(() => Parse(AirportCatalog.ReadResource("countries.txt")));

        public static IReadOnlyList<Country> All => Embedded.Value.List;

        public static IReadOnlyDictionary<string, Country> Lookup => Embedded.Value.Lookup;

        /// <summary>Lines "ISO|Dutch name|other names, comma separated".</summary>
        public static (List<Country> List, Dictionary<string, Country> Lookup) Parse(string text)
        {
            var list = new List<Country>();
            var lookup = new Dictionary<string, Country>();
            foreach (var line in text.Split('\n'))
            {
                if (line.Length == 0 || line[0] == '#') continue;
                var f = line.TrimEnd('\r').Split('|');
                if (f.Length < 2) continue;
                var c = new Country(f[0], f[1]);
                list.Add(c);
                lookup.TryAdd(Key(c.Code), c);
                lookup.TryAdd(Key(c.Name), c);
                if (f.Length > 2)
                    foreach (var alias in f[2].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        lookup.TryAdd(Key(alias), c);
            }
            return (list, lookup);
        }

        /// <summary>The countries in the text, in order, and the words that were not recognised.</summary>
        public static (List<Country> Known, List<string> Unknown) Read(string? text) => Read(text, Embedded.Value.Lookup);

        public static (List<Country> Known, List<string> Unknown) Read(string? text, IReadOnlyDictionary<string, Country> lookup)
        {
            var known = new List<Country>();
            var unknown = new List<string>();
            foreach (var part in (text ?? "").Split(new[] { ',', ';', '\n', '/' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                // "Spanje en Portugal" is two countries.
                foreach (var word in part.Split(" en ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (lookup.TryGetValue(Key(word), out var c)) { if (!known.Contains(c)) known.Add(c); }
                    else unknown.Add(word);
                }
            }
            return (known, unknown);
        }

        /// <summary>Lower case without accents or spaces: "België" and "belgie" are the same.</summary>
        private static string Key(string s)
        {
            var sb = new StringBuilder();
            foreach (var ch in s.Normalize(NormalizationForm.FormD))
                if (char.IsLetter(ch)) sb.Append(char.ToLowerInvariant(ch));
            return sb.ToString();
        }
    }

    /// <summary>
    /// What the tour visits, from the text in the settings: countries ("Nederland, UK"), and also places, islands
    /// or airport codes ("Tenerife", "Mallorca", "TFS"), found in the names of the airports.
    /// </summary>
    public sealed record TourSelection(List<List<TourAirport>> Groups, List<string> Recognised, List<string> Unknown)
    {
        /// <summary>All airports of the selection (each once).</summary>
        public List<TourAirport> Airports => Groups.SelectMany(g => g).Distinct().ToList();

        public static TourSelection From(string? text) => From(text, AirportCatalog.All, Countries.Lookup);

        public static TourSelection From(string? text, IReadOnlyList<TourAirport> all, IReadOnlyDictionary<string, Country> lookup)
        {
            var (known, words) = Countries.Read(text, lookup);
            // One group per country or place typed, so "Nederland, Amerika, Tenerife" gets a third each.
            var groups = known.Select(c => TourPlanner.AirportsIn(all, new[] { c.Code })).ToList();
            var recognised = known.Select(c => c.Name).ToList();
            var unknown = new List<string>();
            foreach (var word in words)
            {
                string key = Words(word);
                var found = key.Length < 3 ? new List<TourAirport>()
                    : all.Where(a => string.Equals(a.Code, word, StringComparison.OrdinalIgnoreCase)
                                     || (key.Length >= 4 && $" {Words(a.Name)} ".Contains($" {key} ", StringComparison.Ordinal)))
                         .ToList();
                if (found.Count == 0) { unknown.Add(word); continue; }
                recognised.Add($"{word} ({string.Join(", ", found.Take(4).Select(a => a.Code))}{(found.Count > 4 ? ", …" : "")})");
                groups.Add(found);
            }
            return new TourSelection(groups, recognised, unknown);
        }

        /// <summary>Lower case words without accents: "Palma de Mallorca Airport" becomes "palma de mallorca airport".</summary>
        private static string Words(string s)
        {
            var sb = new StringBuilder();
            foreach (var ch in s.Normalize(NormalizationForm.FormD))
            {
                if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
                else if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark && (sb.Length == 0 || sb[^1] != ' ')) sb.Append(' ');
            }
            return sb.ToString().Trim();
        }
    }

    public enum SceneKind { Home, Airport, Follow }

    /// <summary>
    /// What the flight screensaver shows for a while: the pin (always), an airport, or one flight followed.
    /// <paramref name="FocusScreen"/> is the screen (counted from the left) the airport or plane is shown on.
    /// </summary>
    public sealed record TourScene(int Number, SceneKind Kind, double Lat, double Lon, double RadiusKm,
                                   DateTime StartUtc, DateTime EndUtc, int FocusScreen, string Title,
                                   string? FollowHex = null, TourAirport? Airport = null)
    {
        public string Key => FormattableString.Invariant($"{Number}|{Kind}|{Lat:0.#####},{Lon:0.#####},{RadiusKm:0.#}");

        /// <summary>How much of the picture is visible: fades in after the start and out before the end.</summary>
        public double Visibility(DateTime utc, double fadeSeconds)
        {
            if (Kind == SceneKind.Home) return 1;
            double fromStart = (utc - StartUtc).TotalSeconds, toEnd = (EndUtc - utc).TotalSeconds;
            return Math.Clamp(Math.Min(fromStart, toEnd) / fadeSeconds, 0, 1);
        }
    }

    /// <summary>The choices of the tour (no clock or network here, so they can be tested).</summary>
    public sealed class TourPlanner
    {
        /// <summary>How many earlier airports are not visited again right away.</summary>
        public const int RememberAirports = 3;

        /// <summary>An airport with fewer planes in the air around it than this is skipped (when there is time).</summary>
        public const int MinFlying = 2;

        /// <summary>Planes in the air (not taxiing or parked).</summary>
        public static int Flying(IEnumerable<AircraftReport> reports) => reports.Count(r => !r.OnGround && (r.AltitudeFeet ?? 0) >= 300);

        private readonly Random _random;
        private readonly Queue<string> _recent = new();

        public TourPlanner(Random random) => _random = random;

        /// <summary>
        /// The airports of these countries (all countries when none are given). Only the big ones, per country,
        /// when it has at least a few; a country with fewer big airports keeps its medium ones too.
        /// </summary>
        public static List<TourAirport> AirportsIn(IReadOnlyList<TourAirport> all, IEnumerable<string> countryCodes)
        {
            var codes = countryCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var list = all.Where(a => codes.Contains(a.Country)).ToList();
            if (list.Count == 0) list = all.ToList();
            return list.GroupBy(a => a.Country)
                       .SelectMany(g => g.Count(a => a.Weight >= BigWeight) >= 3 ? g.Where(a => a.Weight >= BigWeight) : g)
                       .ToList();
        }

        /// <summary>Weight of a big airport in the catalog (medium ones weigh 1).</summary>
        public const double BigWeight = 3;

        /// <summary>
        /// A random group first (each country or place typed gets its turn, but not twice in a row; one where
        /// everything is quiet right now less often), then an airport in it.
        /// </summary>
        public TourAirport PickAirport(IReadOnlyList<IReadOnlyList<TourAirport>> groups)
        {
            var usable = groups.Where(g => g.Count > 0).ToList();
            var other = usable.Where(g => g[0].Code != _lastGroup).ToList();
            var pool = other.Count > 0 ? other : usable;
            double Weight(IReadOnlyList<TourAirport> g) => g.Any(a => !IsQuiet(a.Code)) ? 1 : QuietGroupChance;
            double roll = _random.NextDouble() * pool.Sum(Weight);
            var group = pool[^1];
            foreach (var g in pool)
            {
                roll -= Weight(g);
                if (roll < 0) { group = g; break; }
            }
            _lastGroup = group[0].Code;
            return PickAirport(group);
        }

        /// <summary>Another airport of the same country or place that is not quiet, or null when there is none.</summary>
        public TourAirport? Alternative(IReadOnlyList<IReadOnlyList<TourAirport>> groups, TourAirport quiet)
        {
            var group = groups.FirstOrDefault(g => g.Contains(quiet));
            if (group == null || !group.Any(a => a != quiet && !IsQuiet(a.Code))) return null;
            return PickAirport(group.Where(a => a != quiet).ToList());
        }

        private string? _lastGroup;

        /// <summary>Airports where hardly anything flew, and until when they are left alone.</summary>
        private readonly Dictionary<string, DateTime> _quiet = new();

        /// <summary>How long an airport that turned out quiet is skipped.</summary>
        public static readonly TimeSpan QuietFor = TimeSpan.FromMinutes(20);

        /// <summary>How often a country or place where everything is quiet still gets its turn, relative to the others.</summary>
        public const double QuietGroupChance = 0.35;

        /// <summary>The clock (replaceable in tests).</summary>
        public Func<DateTime> Now { get; set; } = () => DateTime.UtcNow;

        public void MarkQuiet(string code) => _quiet[code] = Now() + QuietFor;

        public bool IsQuiet(string code) => _quiet.TryGetValue(code, out var until) && until > Now();

        /// <summary>A random airport, busy ones more often, not one of the last few.</summary>
        public TourAirport PickAirport(IReadOnlyList<TourAirport> airports)
        {
            var awake = airports.Where(a => !IsQuiet(a.Code)).ToList();
            if (awake.Count > 0) airports = awake;
            var choice = airports.Where(a => !_recent.Contains(a.Code)).ToList();
            // All shown just now: anything but the very last one (when there is a choice at all).
            if (choice.Count == 0) choice = airports.Where(a => a.Code != _recent.LastOrDefault()).ToList();
            if (choice.Count == 0) choice = airports.ToList();
            double total = choice.Sum(a => a.Weight), roll = _random.NextDouble() * total;
            var pick = choice[^1];
            foreach (var a in choice)
            {
                roll -= a.Weight;
                if (roll < 0) { pick = a; break; }
            }
            _recent.Enqueue(pick.Code);
            while (_recent.Count > RememberAirports) _recent.Dequeue();
            return pick;
        }

        /// <summary>Every third scene follows a flight (when that is switched on and there is one).</summary>
        public static bool IsFollowTurn(int number, bool followFlights) => followFlights && number % 3 == 2;

        /// <summary>
        /// A flight worth following near (lat, lon): in the air and flying (not climbing out at walking pace),
        /// with an airline call sign. Flights whose route is known are preferred.
        /// </summary>
        public AircraftReport? PickFlight(IEnumerable<AircraftReport> reports, double lat, double lon, double maxKm, Func<string, bool> hasRoute)
        {
            var projection = new LocalProjection(lat, lon);
            var candidates = reports.Where(r => !r.OnGround && r.GroundSpeedKnots >= 150 && (r.AltitudeFeet ?? 0) >= 3000
                                                && RouteParser.IsAirlineCallsign(r.Callsign))
                                    .Where(r =>
                                    {
                                        var (x, y) = projection.ToKm(r.Lat, r.Lon);
                                        return Math.Sqrt(x * x + y * y) <= maxKm;
                                    })
                                    .ToList();
            if (candidates.Count == 0) return null;
            var withRoute = candidates.Where(r => hasRoute(r.Callsign)).ToList();
            var pool = withRoute.Count > 0 ? withRoute : candidates;
            return pool[_random.Next(pool.Count)];
        }
    }

    /// <summary>
    /// Runs the tour for all screens together: every screen asks which scene it is, so they change at the same
    /// moment. The next scene is chosen a few seconds early, and its map and planes are fetched in the meantime,
    /// so after the fade there is something to see straight away.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public static class FlightTour
    {
        public const double AirportRadiusKm = 22;
        public const double FollowRadiusKm = 16;
        public const double FadeSeconds = 1.2;
        private static readonly TimeSpan PrepareAhead = TimeSpan.FromSeconds(8);
        private static readonly TimeSpan LastMomentToPlan = TimeSpan.FromSeconds(3);
        /// <summary>Until this long before the switch a quiet next airport may still be swapped for another.</summary>
        private static readonly TimeSpan LastMomentToSwap = TimeSpan.FromSeconds(2.5);
        private const int MaxSwaps = 3;
        private static bool _nextChecked;
        private static int _swaps;

        private static readonly object Gate = new();
        private static readonly TourPlanner Planner = new(new Random());
        private static int _users;
        private static TourScene? _current, _next;
        private static IDisposable? _prepared;
        private static string _settingsKey = "";

        /// <summary>A screen (or the preview) takes part; when the last one leaves, the tour starts afresh next time.</summary>
        public static IDisposable Attach()
        {
            lock (Gate) _users++;
            return new Leave();
        }

        private sealed class Leave : IDisposable
        {
            private bool _done;
            public void Dispose()
            {
                if (_done) return;
                _done = true;
                lock (Gate)
                {
                    if (--_users > 0) return;
                    Reset();
                }
            }
        }

        private static void Reset()
        {
            _current = _next = null;
            _nextChecked = false;
            _swaps = 0;
            _prepared?.Dispose();
            _prepared = null;
        }

        /// <summary>
        /// Start fetching the first scene (map and planes) during the fade to black. For the pin the feed is held
        /// by the returned subscription; a tour scene is prepared by the tour itself.
        /// </summary>
        public static IDisposable WarmUp(FlightsSettings s)
        {
            var use = Attach();
            var scene = SceneAt(DateTime.UtcNow, s);
            if (scene.Kind != SceneKind.Home) return use;
            _ = MapLoader.GetAsync(scene.Lat, scene.Lon, scene.RadiusKm, s.CacheDirectory);
            var feed = AircraftFeed.Subscribe(scene.Lat, scene.Lon, FlightsView.FeedRadiusKm(scene.RadiusKm));
            return new Both(use, feed);
        }

        private sealed class Both(IDisposable a, IDisposable b) : IDisposable
        {
            public void Dispose() { b.Dispose(); a.Dispose(); }
        }

        /// <summary>The pin scene, shown always when the tour is off.</summary>
        public static TourScene HomeScene(FlightsSettings s) =>
            new(0, SceneKind.Home, s.Latitude, s.Longitude, s.RadiusKm, DateTime.MinValue, DateTime.MaxValue, 0, s.PlaceName);

        /// <summary>The scene for this moment (and, near its end, the next one is prepared).</summary>
        public static TourScene SceneAt(DateTime now, FlightsSettings s)
        {
            if (s.Mode != FlightsMode.Tour) return HomeScene(s);
            lock (Gate)
            {
                string key = $"{s.TourCountries}|{s.FollowFlights}|{s.SceneSeconds}";
                if (key != _settingsKey) { _settingsKey = key; Reset(); }

                if (_current == null)
                {
                    _current = AirportScene(0, now, s);
                    Prepare(_current, s);
                }
                if (_next == null && now >= _current.EndUtc - PrepareAhead)
                {
                    // A flight to follow needs the planes of this scene; when they are not in yet, wait a little.
                    bool lastChance = now >= _current.EndUtc - LastMomentToPlan;
                    _next = Plan(_current, s, waitForFlight: !lastChance);
                    if (_next != null) Prepare(_next, s);
                }
                CheckNextIsBusy(now, s);
                if (now >= _current.EndUtc)
                {
                    // Long gap (the computer slept): start the next scene now instead of in the past.
                    var next = _next ?? Plan(_current, s, waitForFlight: false)!;
                    _current = now - next.EndUtc > TimeSpan.Zero ? next with { StartUtc = now, EndUtc = now + Duration(s) } : next;
                    _next = null;
                    _nextChecked = false;
                    _swaps = 0;
                }
                return _current;
            }
        }

        /// <summary>
        /// As soon as the planes around the next airport are in: when hardly anything flies there (night, a quiet
        /// field), take another airport instead, a few times at most and only while there is time to fetch it.
        /// </summary>
        private static void CheckNextIsBusy(DateTime now, FlightsSettings s)
        {
            // The airport on screen now (e.g. the very first one) turns out quiet: not again for a while.
            if (_current is { Kind: SceneKind.Airport, Airport: { } shown } && !Planner.IsQuiet(shown.Code)
                && AircraftFeed.RecentFor(_current.Lat, _current.Lon) is { } current && TourPlanner.Flying(current) < TourPlanner.MinFlying)
                Planner.MarkQuiet(shown.Code);

            if (_next is not { Kind: SceneKind.Airport } next || _nextChecked || _current == null) return;
            if (now >= _current.EndUtc - LastMomentToSwap) { _nextChecked = true; return; }
            if (AircraftFeed.RecentFor(next.Lat, next.Lon) is not { } reports) return; // not in yet
            int flying = TourPlanner.Flying(reports);
            if (flying >= TourPlanner.MinFlying || _swaps >= MaxSwaps) { _nextChecked = true; return; }
            if (next.Airport == null) { _nextChecked = true; return; }
            Planner.MarkQuiet(next.Airport.Code);
            // Another airport of the same country or place; none (Tenerife, both quiet): show it anyway.
            var other = Planner.Alternative(Groups(s), next.Airport);
            Serilog.Log.Debug("Tour: {Code} is quiet ({Flying} in the air), {Action}.", next.Airport.Code, flying, other != null ? $"taking {other.Code}" : "showing it anyway");
            if (other == null) { _nextChecked = true; return; }
            _swaps++;
            _next = AirportSceneAt(next.Number, next.StartUtc, s, other);
            Prepare(_next, s);
        }

        private static TimeSpan Duration(FlightsSettings s) => TimeSpan.FromSeconds(s.SceneSeconds);

        /// <summary>The next scene; null when it should follow a flight but the planes are not in yet (ask again).</summary>
        private static TourScene? Plan(TourScene previous, FlightsSettings s, bool waitForFlight)
        {
            int number = previous.Number + 1;
            var start = previous.EndUtc;
            if (TourPlanner.IsFollowTurn(number, s.FollowFlights))
            {
                var reports = AircraftFeed.RecentFor(previous.Lat, previous.Lon);
                var flight = reports == null ? null
                    : Planner.PickFlight(reports, previous.Lat, previous.Lon, previous.RadiusKm * 1.2, cs => RouteLookup.For(cs) != null);
                if (flight != null)
                    return new TourScene(number, SceneKind.Follow, flight.Lat, flight.Lon, FollowRadiusKm, start, start + Duration(s),
                                         number, flight.Callsign, flight.Hex);
                if (waitForFlight) return null;
            }
            return AirportScene(number, start, s);
        }

        private static TourScene AirportScene(int number, DateTime start, FlightsSettings s)
        {
            return AirportSceneAt(number, start, s, Planner.PickAirport(Groups(s)));
        }

        private static List<IReadOnlyList<TourAirport>> Groups(FlightsSettings s)
        {
            var groups = TourSelection.From(s.TourCountries).Groups.Where(g => g.Count > 0).ToList<IReadOnlyList<TourAirport>>();
            if (groups.Count == 0) groups.Add(TourPlanner.AirportsIn(AirportCatalog.All, new[] { "NL" }));
            return groups;
        }

        private static TourScene AirportSceneAt(int number, DateTime start, FlightsSettings s, TourAirport airport)
        {
            return new TourScene(number, SceneKind.Airport, airport.Lat, airport.Lon, AirportRadiusKm, start, start + Duration(s),
                                 number, airport.Name, Airport: airport);
        }

        /// <summary>Fetch the map and the planes of a scene before it is shown.</summary>
        private static void Prepare(TourScene scene, FlightsSettings s)
        {
            _ = MapLoader.GetAsync(scene.Lat, scene.Lon, scene.RadiusKm, s.CacheDirectory);
            var old = _prepared;
            _prepared = AircraftFeed.Subscribe(scene.Lat, scene.Lon, FlightsView.FeedRadiusKm(scene.RadiusKm));
            old?.Dispose();
        }
    }
}
