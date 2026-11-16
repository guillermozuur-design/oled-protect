using OLED_Sleeper.Storage.Interfaces;
using System.IO;

namespace OLED_Sleeper.Features.Flights
{
    /// <summary>
    /// Settings for the "Vliegtuigen" screensaver: where the pin is, how far around it the map reaches and
    /// whether the planes get a label. Remembered in <c>flights.json</c>, only on this computer.
    /// </summary>
    public class FlightsSettings
    {
        private const string FileName = "flights.json";

        /// <summary>Default pin: the middle of the Netherlands (Utrecht), until the user picks a place.</summary>
        public const double DefaultLatitude = 52.0907, DefaultLongitude = 5.1214;
        public const string DefaultPlaceName = "Utrecht";
        public const double DefaultRadiusKm = 40, MinRadiusKm = 15, MaxRadiusKm = 80;

        public sealed class Stored
        {
            public double Latitude { get; set; } = DefaultLatitude;
            public double Longitude { get; set; } = DefaultLongitude;
            public string PlaceName { get; set; } = DefaultPlaceName;
            public double RadiusKm { get; set; } = DefaultRadiusKm;
            public bool ShowLabels { get; set; } = true;
        }

        private readonly IAppDataFileStore? _fileStore;
        private Stored _value;

        public FlightsSettings(IAppDataFileStore? fileStore, IStorageRoot? storageRoot = null)
        {
            _fileStore = fileStore;
            _value = Sanitize(_fileStore?.Read<Stored>(FileName) ?? new Stored());
            CacheDirectory = storageRoot != null ? Path.Combine(storageRoot.DirectoryPath, "map-cache") : null;
        }

        /// <summary>Fires when anything changed.</summary>
        public event Action? Changed;

        /// <summary>Where downloaded map tiles are kept (null: do not keep them).</summary>
        public string? CacheDirectory { get; }

        public double Latitude => _value.Latitude;
        public double Longitude => _value.Longitude;
        public string PlaceName => _value.PlaceName;
        public double RadiusKm => _value.RadiusKm;
        public bool ShowLabels => _value.ShowLabels;

        public void SetLocation(double latitude, double longitude, string placeName) =>
            Update(v => { v.Latitude = latitude; v.Longitude = longitude; v.PlaceName = placeName; });

        public void SetRadius(double radiusKm) => Update(v => v.RadiusKm = radiusKm);

        public void SetShowLabels(bool show) => Update(v => v.ShowLabels = show);

        private void Update(Action<Stored> change)
        {
            var copy = new Stored
            {
                Latitude = _value.Latitude,
                Longitude = _value.Longitude,
                PlaceName = _value.PlaceName,
                RadiusKm = _value.RadiusKm,
                ShowLabels = _value.ShowLabels
            };
            change(copy);
            copy = Sanitize(copy);
            if (copy.Latitude == _value.Latitude && copy.Longitude == _value.Longitude && copy.PlaceName == _value.PlaceName &&
                Math.Abs(copy.RadiusKm - _value.RadiusKm) < 0.01 && copy.ShowLabels == _value.ShowLabels) return;
            _value = copy;
            _fileStore?.TryWrite(FileName, _value);
            Changed?.Invoke();
        }

        private static Stored Sanitize(Stored v)
        {
            if (double.IsNaN(v.Latitude) || Math.Abs(v.Latitude) > 85 || double.IsNaN(v.Longitude) || Math.Abs(v.Longitude) > 180)
            {
                v.Latitude = DefaultLatitude;
                v.Longitude = DefaultLongitude;
                v.PlaceName = DefaultPlaceName;
            }
            v.PlaceName = string.IsNullOrWhiteSpace(v.PlaceName) ? $"{v.Latitude:0.###}, {v.Longitude:0.###}" : v.PlaceName.Trim();
            v.RadiusKm = double.IsNaN(v.RadiusKm) ? DefaultRadiusKm : Math.Clamp(v.RadiusKm, MinRadiusKm, MaxRadiusKm);
            return v;
        }
    }
}
