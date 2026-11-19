using OledProtect.Storage.Interfaces;
using System.IO;

namespace OledProtect.Features.Flights
{
    /// <summary>Around the pin only, or a tour along busy airports (and now and then one flight followed).</summary>
    public enum FlightsMode { Home, Tour }

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
        public const string DefaultTourCountries = "Nederland, België, Duitsland, Frankrijk, Verenigd Koninkrijk";
        public const int DefaultSceneSeconds = 20, MinSceneSeconds = 10, MaxSceneSeconds = 60;

        public sealed class Stored
        {
            public double Latitude { get; set; } = DefaultLatitude;
            public double Longitude { get; set; } = DefaultLongitude;
            public string PlaceName { get; set; } = DefaultPlaceName;
            public double RadiusKm { get; set; } = DefaultRadiusKm;
            public bool ShowLabels { get; set; } = true;
            public FlightsMode Mode { get; set; } = FlightsMode.Home;
            public string TourCountries { get; set; } = DefaultTourCountries;
            public bool FollowFlights { get; set; } = true;
            public int SceneSeconds { get; set; } = DefaultSceneSeconds;

            public Stored Copy() => (Stored)MemberwiseClone();

            public bool SameAs(Stored o) =>
                Latitude == o.Latitude && Longitude == o.Longitude && PlaceName == o.PlaceName && Math.Abs(RadiusKm - o.RadiusKm) < 0.01 &&
                ShowLabels == o.ShowLabels && Mode == o.Mode && TourCountries == o.TourCountries && FollowFlights == o.FollowFlights &&
                SceneSeconds == o.SceneSeconds;
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
        public FlightsMode Mode => _value.Mode;
        public string TourCountries => _value.TourCountries;
        public bool FollowFlights => _value.FollowFlights;
        public int SceneSeconds => _value.SceneSeconds;

        public void SetLocation(double latitude, double longitude, string placeName) =>
            Update(v => { v.Latitude = latitude; v.Longitude = longitude; v.PlaceName = placeName; });

        public void SetRadius(double radiusKm) => Update(v => v.RadiusKm = radiusKm);

        public void SetShowLabels(bool show) => Update(v => v.ShowLabels = show);

        public void SetMode(FlightsMode mode) => Update(v => v.Mode = mode);

        public void SetTourCountries(string countries) => Update(v => v.TourCountries = countries);

        public void SetFollowFlights(bool follow) => Update(v => v.FollowFlights = follow);

        public void SetSceneSeconds(int seconds) => Update(v => v.SceneSeconds = seconds);

        private void Update(Action<Stored> change)
        {
            var copy = _value.Copy();
            change(copy);
            copy = Sanitize(copy);
            if (copy.SameAs(_value)) return;
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
            if (!Enum.IsDefined(v.Mode)) v.Mode = FlightsMode.Home;
            v.TourCountries = (v.TourCountries ?? DefaultTourCountries).Trim();
            v.SceneSeconds = Math.Clamp(v.SceneSeconds, MinSceneSeconds, MaxSceneSeconds);
            return v;
        }
    }
}
