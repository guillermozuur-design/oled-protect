using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OLED_Sleeper.Features.Flights
{
    /// <summary>Finds the coordinates of a place, address or postcode, or reads typed coordinates.</summary>
    public static partial class PlaceSearch
    {
        public sealed record Result(double Latitude, double Longitude, string Name);

        [GeneratedRegex(@"^\s*(-?\d{1,2}(?:[.,]\d+)?)\s*[,; ]\s*(-?\d{1,3}(?:[.,]\d+)?)\s*$")]
        private static partial Regex CoordinatePattern();

        /// <summary>"52.09, 5.12" or "52,09; 5,12" → coordinates (null when the text is not coordinates).</summary>
        public static Result? ParseCoordinates(string text)
        {
            var m = CoordinatePattern().Match(text);
            if (!m.Success) return null;
            // With a comma as separator between the two numbers, a comma inside a number is a decimal comma.
            static double Num(string s) => double.Parse(s.Replace(',', '.'), CultureInfo.InvariantCulture);
            double lat = Num(m.Groups[1].Value), lon = Num(m.Groups[2].Value);
            if (Math.Abs(lat) > 85 || Math.Abs(lon) > 180) return null;
            return new Result(lat, lon, string.Create(CultureInfo.InvariantCulture, $"{lat:0.####}, {lon:0.####}"));
        }

        /// <summary>Short display name from a Nominatim result: the place itself plus its municipality or region.</summary>
        public static string ShortName(string displayName)
        {
            var parts = displayName.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length <= 2) return string.Join(", ", parts);
            return $"{parts[0]}, {parts[1]}";
        }

        /// <summary>Looks the text up with OpenStreetMap's Nominatim (one request, only when the user searches).</summary>
        [ExcludeFromCodeCoverage]
        public static async Task<Result?> FindAsync(string text, CancellationToken token = default)
        {
            if (ParseCoordinates(text) is { } coordinates) return coordinates;

            using var http = AircraftFeed.CreateClient();
            string url = "https://nominatim.openstreetmap.org/search?format=jsonv2&limit=1&accept-language=nl&q=" + Uri.EscapeDataString(text.Trim());
            using var doc = JsonDocument.Parse(await http.GetStringAsync(url, token));
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0) return null;
            var first = doc.RootElement[0];
            double lat = double.Parse(first.GetProperty("lat").GetString()!, CultureInfo.InvariantCulture);
            double lon = double.Parse(first.GetProperty("lon").GetString()!, CultureInfo.InvariantCulture);
            string name = first.TryGetProperty("display_name", out var d) ? ShortName(d.GetString() ?? text) : text;
            return new Result(lat, lon, name);
        }
    }
}
