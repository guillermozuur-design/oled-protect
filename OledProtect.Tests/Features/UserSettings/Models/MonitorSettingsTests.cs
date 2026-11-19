using OledProtect.Features.MonitorBehavior.Models;
using OledProtect.Features.UserSettings.Models;
using OledProtect.UI.Models;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OledProtect.Tests.Features.UserSettings.Models
{
    public class MonitorSettingsTests
    {
        [Theory]
        [InlineData(1, TimeUnit.Seconds, 1_000)]
        [InlineData(45, TimeUnit.Seconds, 45_000)]
        [InlineData(3, TimeUnit.Minutes, 180_000)]
        [InlineData(1, TimeUnit.Hours, 3_600_000)]
        [InlineData(0, TimeUnit.Hours, 0)]
        public void Idle_time_in_milliseconds_follows_value_and_unit(int value, TimeUnit unit, int expected)
        {
            Assert.Equal(expected, new MonitorSettings { IdleValue = value, IdleUnit = unit }.IdleTimeMilliseconds);
        }

        [Fact]
        public void An_empty_idle_field_means_zero_milliseconds()
        {
            Assert.Equal(0, new MonitorSettings { IdleValue = null, IdleUnit = TimeUnit.Hours }.IdleTimeMilliseconds);
        }

        [Fact]
        public void A_new_monitor_starts_with_these_values()
        {
            var fresh = new MonitorSettings();

            var expected = new Dictionary<string, object?>
            {
                ["HardwareId"] = "",
                ["IsManaged"] = false,
                ["Behavior"] = MonitorBehaviorType.None,
                ["DimLevel"] = 15d,
                ["LowerBrightnessOnBlackout"] = false,
                ["IdleValue"] = 30,
                ["IdleUnit"] = TimeUnit.Seconds,
                ["IsActiveOnInput"] = false,
                ["IsActiveOnMousePosition"] = true,
                ["IsActiveOnActiveWindow"] = false,
                ["UseBrightnessSchedule"] = false,
            };

            foreach (var (property, value) in expected)
                Assert.True(Equals(value, typeof(MonitorSettings).GetProperty(property)!.GetValue(fresh)), property);
        }

        [Fact]
        public void The_default_schedule_has_morning_evening_and_night_steps()
        {
            var steps = new MonitorSettings().BrightnessSchedule;

            Assert.Collection(steps,
                s => Assert.Equal(("08:00", 80), (s.Start, s.Brightness)),
                s => Assert.Equal(("19:00", 50), (s.Start, s.Brightness)),
                s => Assert.Equal(("23:00", 30), (s.Start, s.Brightness)));
        }

        [Fact]
        public void Each_monitor_gets_its_own_schedule_list()
        {
            var a = new MonitorSettings();
            var b = new MonitorSettings();

            a.BrightnessSchedule.RemoveAt(0);

            Assert.NotSame(a.BrightnessSchedule, b.BrightnessSchedule);
            Assert.Equal(3, b.BrightnessSchedule.Count);
        }

        [Fact]
        public void A_blank_schedule_step_starts_at_eight_at_seventy_percent()
        {
            var step = new BrightnessScheduleEntry();

            Assert.Equal(("08:00", 70), (step.Start, step.Brightness));
        }

        // Settings files written by earlier versions must keep loading, including keys that were added later.
        [Fact]
        public void An_older_settings_file_is_read_correctly()
        {
            const string stored = """
                {"SchemaVersion":1,"Monitors":[{"HardwareId":"GSM5B7F","IsManaged":true,"Behavior":1,
                 "DimLevel":22,"IdleValue":2,"IdleUnit":2,"IsActiveOnInput":false,
                 "IsActiveOnMousePosition":true,"IsActiveOnActiveWindow":false,"IdleTimeMilliseconds":7200000}]}
                """;

            var document = JsonSerializer.Deserialize<MonitorSettingsDocument>(stored)!;

            Assert.Equal(1, document.SchemaVersion);
            var screen = Assert.Single(document.Monitors);
            Assert.Equal("GSM5B7F", screen.HardwareId);
            Assert.True(screen.IsManaged);
            Assert.Equal((MonitorBehaviorType)1, screen.Behavior);
            Assert.Equal(22, screen.DimLevel);
            Assert.Equal(TimeUnit.Hours, screen.IdleUnit);
            Assert.Equal(7_200_000, screen.IdleTimeMilliseconds);
            Assert.False(screen.IsActiveOnInput);
            Assert.True(screen.IsActiveOnMousePosition);
            Assert.False(screen.IsActiveOnActiveWindow);
            Assert.False(screen.LowerBrightnessOnBlackout);
            Assert.Equal(3, screen.BrightnessSchedule.Count);
        }

        [Fact]
        public void Stored_json_uses_the_established_key_names()
        {
            var keys = JsonNode.Parse(JsonSerializer.Serialize(new MonitorSettings()))!.AsObject().Select(p => p.Key);

            Assert.Equal(
                new[]
                {
                    "HardwareId", "IsManaged", "Behavior", "DimLevel", "LowerBrightnessOnBlackout", "IdleValue",
                    "IdleUnit", "IsActiveOnInput", "IsActiveOnMousePosition", "IsActiveOnActiveWindow", "UseBrightnessSchedule", "BrightnessSchedule",
                    "IdleTimeMilliseconds",
                }.Order(),
                keys.Order());
        }

        [Fact]
        public void Every_value_survives_a_save_and_load()
        {
            var before = new MonitorSettings
            {
                HardwareId = "AUS27",
                IsManaged = true,
                Behavior = (MonitorBehaviorType)2,
                DimLevel = 48,
                LowerBrightnessOnBlackout = true,
                IdleValue = 12,
                IdleUnit = TimeUnit.Minutes,
                IsActiveOnInput = true,
                IsActiveOnMousePosition = false,
                IsActiveOnActiveWindow = true,
                UseBrightnessSchedule = true,
                BrightnessSchedule = [new() { Start = "21:15", Brightness = 5 }, new() { Start = "07:45", Brightness = 90 }],
            };

            string firstPass = JsonSerializer.Serialize(before);
            string secondPass = JsonSerializer.Serialize(JsonSerializer.Deserialize<MonitorSettings>(firstPass));

            Assert.Equal(firstPass, secondPass);
        }
    }
}
