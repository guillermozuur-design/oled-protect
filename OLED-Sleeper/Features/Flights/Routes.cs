using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OLED_Sleeper.Features.Flights
{
    /// <summary>Where a flight comes from and goes to, as airport codes ("RTM", "TFS").</summary>
    public sealed record FlightRoute(string From, string To)
    {
        public override string ToString() => $"{From}→{To}";
    }

    /// <summary>Reading the route answers of adsbdb.com (no network here, so it can be tested).</summary>
    public static partial class RouteParser
    {
        /// <summary>
        /// Airline flights have a call sign like KLM1234 or EZY29YK; only those have a known route. Private planes
        /// (PHABC) and the like are not looked up.
        /// </summary>
        public static bool IsAirlineCallsign(string callsign) => AirlineCallsign().IsMatch(callsign);

        /// <summary>The route in an adsbdb answer, or null when the call sign is unknown.</summary>
        public static FlightRoute? Parse(string json)
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("response", out var response) || response.ValueKind != JsonValueKind.Object) return null;
            if (!response.TryGetProperty("flightroute", out var route) || route.ValueKind != JsonValueKind.Object) return null;
            string from = Code(route, "origin"), to = Code(route, "destination");
            return from.Length > 0 && to.Length > 0 ? new FlightRoute(from, to) : null;
        }

        private static string Code(JsonElement route, string end)
        {
            if (!route.TryGetProperty(end, out var airport) || airport.ValueKind != JsonValueKind.Object) return "";
            foreach (var key in new[] { "iata_code", "icao_code" })
                if (airport.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 } code)
                    return code;
            return "";
        }

        [GeneratedRegex("^[A-Z]{3}[0-9][0-9A-Z]{0,4}$")]
        private static partial Regex AirlineCallsign();
    }

    /// <summary>
    /// Looks up the route of a flight by its call sign at adsbdb.com (free, open data), once per call sign:
    /// answers are kept for hours, so a screensaver asks only for the flights it has not seen yet. Calm on the
    /// service: two questions at a time, and it rests a while after an error.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public static class RouteLookup
    {
        private static readonly TimeSpan KeepKnown = TimeSpan.FromHours(6);
        private static readonly TimeSpan KeepUnknown = TimeSpan.FromHours(1);
        private static readonly HttpClient Http = AircraftFeed.CreateClient();
        private static readonly ConcurrentDictionary<string, (FlightRoute? Route, DateTime Until)> Cache = new();
        private static readonly ConcurrentDictionary<string, byte> Asking = new();
        /// <summary>One question at a time, about one a second, and not more than this many waiting.</summary>
        private static readonly SemaphoreSlim Slots = new(1);
        private static readonly TimeSpan Pause = TimeSpan.FromSeconds(1);
        private const int MaxWaiting = 20;
        private const int MaxCached = 5000;
        private static DateTime _restUntil = DateTime.MinValue;

        /// <summary>The route when it is known; otherwise null, and it is looked up in the background.</summary>
        public static FlightRoute? For(string callsign)
        {
            if (!RouteParser.IsAirlineCallsign(callsign)) return null;
            var now = DateTime.UtcNow;
            if (Cache.TryGetValue(callsign, out var hit) && hit.Until > now) return hit.Route;
            if (now >= _restUntil && Asking.Count < MaxWaiting && Asking.TryAdd(callsign, 0)) _ = AskAsync(callsign);
            return hit.Route; // an old answer is still better than nothing while a new one comes
        }

        private static async Task AskAsync(string callsign)
        {
            await Slots.WaitAsync();
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                using var response = await Http.GetAsync($"https://api.adsbdb.com/v0/callsign/{Uri.EscapeDataString(callsign)}", cts.Token);
                if ((int)response.StatusCode == 404)
                {
                    Cache[callsign] = (null, DateTime.UtcNow + KeepUnknown);
                    return;
                }
                if (!response.IsSuccessStatusCode)
                {
                    _restUntil = DateTime.UtcNow + TimeSpan.FromSeconds((int)response.StatusCode == 429 ? 120 : 30);
                    Serilog.Log.Debug("Route lookup refused: {Status}", (int)response.StatusCode);
                    return;
                }
                var route = RouteParser.Parse(await response.Content.ReadAsStringAsync(cts.Token));
                Cache[callsign] = (route, DateTime.UtcNow + (route != null ? KeepKnown : KeepUnknown));
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                _restUntil = DateTime.UtcNow + TimeSpan.FromSeconds(30);
                Serilog.Log.Debug("Route lookup failed: {Message}", ex.Message);
            }
            finally
            {
                if (Cache.Count > MaxCached) Tidy();
                await Task.Delay(Pause);
                Slots.Release();
                Asking.TryRemove(callsign, out _);
            }
        }

        /// <summary>Forget answers that are out of date (the list would otherwise only grow).</summary>
        private static void Tidy()
        {
            var now = DateTime.UtcNow;
            foreach (var old in Cache.Where(kv => kv.Value.Until <= now).Select(kv => kv.Key).ToList())
                Cache.TryRemove(old, out _);
        }
    }
}
