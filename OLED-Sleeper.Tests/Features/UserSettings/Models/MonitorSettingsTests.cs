using OLED_Sleeper.Features.MonitorBehavior.Models;
using OLED_Sleeper.Features.UserSettings.Models;
using OLED_Sleeper.UI.Models;
using System.Text.Json;

namespace OLED_Sleeper.Tests.Features.UserSettings.Models
{
    public class MonitorSettingsTests
    {
        [Theory]
        [InlineData(30, TimeUnit.Seconds, 30_000)]
        [InlineData(5, TimeUnit.Minutes, 300_000)]
        [InlineData(2, TimeUnit.Hours, 7_200_000)]
        [InlineData(null, TimeUnit.Minutes, 0)]
        public void Idle_timeout_is_converted_to_milliseconds(int? value, TimeUnit unit, int expected)
        {
            var settings = new MonitorSettings { IdleValue = value, IdleUnit = unit };

            Assert.Equal(expected, settings.IdleTimeMilliseconds);
        }

        [Fact]
        public void Fresh_settings_use_the_documented_defaults()
        {
            var s = new MonitorSettings();

            Assert.Equal("", s.HardwareId);
            Assert.False(s.IsManaged);
            Assert.Equal(MonitorBehaviorType.None, s.Behavior);
            Assert.Equal(15, s.DimLevel);
            Assert.False(s.LowerBrightnessOnBlackout);
            Assert.Equal(30, s.IdleValue);
            Assert.Equal(TimeUnit.Seconds, s.IdleUnit);
            Assert.False(s.IsActiveOnInput);
            Assert.True(s.IsActiveOnMousePosition);
            Assert.False(s.UseMouseStillLimit);
            Assert.Equal(10, s.MouseStillMinutes);
            Assert.False(s.IsActiveOnActiveWindow);
            Assert.False(s.UseBrightnessSchedule);
            Assert.Equal(["08:00/80", "19:00/50", "23:00/30"], s.BrightnessSchedule.Select(e => $"{e.Start}/{e.Brightness}"));
        }

        [Fact]
        public void Default_schedule_is_not_shared_between_instances()
        {
            var first = new MonitorSettings();
            first.BrightnessSchedule.Clear();

            Assert.Equal(3, new MonitorSettings().BrightnessSchedule.Count);
        }

        [Fact]
        public void New_schedule_entry_has_defaults()
        {
            var entry = new BrightnessScheduleEntry();

            Assert.Equal("08:00", entry.Start);
            Assert.Equal(70, entry.Brightness);
        }

        // Guards the on-disk format: existing users' settings.json files must keep loading.
        [Fact]
        public void Existing_settings_file_still_deserializes()
        {
            const string json = """
                {
                  "SchemaVersion": 1,
                  "Monitors": [
                    {
                      "HardwareId": "DEL1234",
                      "IsManaged": true,
                      "Behavior": 2,
                      "DimLevel": 40,
                      "IdleValue": 5,
                      "IdleUnit": 1,
                      "IsActiveOnInput": true,
                      "IsActiveOnMousePosition": false,
                      "IsActiveOnActiveWindow": true,
                      "IdleTimeMilliseconds": 300000
                    }
                  ]
                }
                """;

            var doc = JsonSerializer.Deserialize<MonitorSettingsDocument>(json)!;
            var m = Assert.Single(doc.Monitors);

            Assert.Equal(1, doc.SchemaVersion);
            Assert.Equal("DEL1234", m.HardwareId);
            Assert.True(m.IsManaged);
            Assert.Equal((MonitorBehaviorType)2, m.Behavior);
            Assert.Equal(40, m.DimLevel);
            Assert.Equal(TimeUnit.Minutes, m.IdleUnit);
            Assert.Equal(300_000, m.IdleTimeMilliseconds);
            Assert.True(m.IsActiveOnInput);
            Assert.False(m.IsActiveOnMousePosition);
            Assert.True(m.IsActiveOnActiveWindow);
            // Keys added later are absent from such a file and fall back to their defaults.
            Assert.False(m.UseBrightnessSchedule);
            Assert.Equal(10, m.MouseStillMinutes);
        }

        [Fact]
        public void Round_trip_keeps_every_stored_value()
        {
            var original = new MonitorSettings
            {
                HardwareId = "X",
                IsManaged = true,
                Behavior = (MonitorBehaviorType)1,
                DimLevel = 33,
                LowerBrightnessOnBlackout = true,
                IdleValue = 7,
                IdleUnit = TimeUnit.Hours,
                IsActiveOnInput = true,
                IsActiveOnMousePosition = false,
                UseMouseStillLimit = true,
                MouseStillMinutes = 3,
                IsActiveOnActiveWindow = true,
                UseBrightnessSchedule = true,
                BrightnessSchedule = [new() { Start = "06:30", Brightness = 12 }],
            };

            var copy = JsonSerializer.Deserialize<MonitorSettings>(JsonSerializer.Serialize(original))!;

            Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(copy));
        }
    }
}
