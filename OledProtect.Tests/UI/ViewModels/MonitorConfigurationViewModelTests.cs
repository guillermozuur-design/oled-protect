using OledProtect.Features.MonitorBehavior.Models;
using OledProtect.Features.MonitorInformation.Models;
using OledProtect.Features.UserSettings.Models;
using OledProtect.UI.Models;
using OledProtect.UI.ViewModels;
using System.Windows;

namespace OledProtect.Tests.UI.ViewModels
{
    public class MonitorConfigurationViewModelTests
    {
        // ---- defaults and round trip ----

        [Fact]
        public void A_new_panel_shows_the_stored_defaults_and_is_clean()
        {
            var sut = Panel();
            var defaults = new MonitorSettings();

            Assert.False(sut.IsDirty);
            Assert.Equal(defaults.IsManaged, sut.IsManaged);
            Assert.Equal(defaults.Behavior, sut.Behavior);
            Assert.Equal(defaults.DimLevel, sut.DimLevel);
            Assert.Equal(defaults.IdleValue, sut.IdleValue);
            Assert.Equal(defaults.IdleUnit, sut.SelectedTimeUnit);
            Assert.Equal(defaults.IsActiveOnMousePosition, sut.IsActiveOnMousePosition);
        }

        [Fact]
        public void Applied_settings_come_back_out_unchanged()
        {
            var stored = Managed();
            stored.UseBrightnessSchedule = true;
            stored.BrightnessSchedule = [new() { Start = "07:30", Brightness = 90 }, new() { Start = "22:00", Brightness = 20 }];
            var sut = Panel(ddc: true, id: "HW-7");

            sut.ApplySettings(stored);
            var result = sut.ToSettings();

            Assert.Equal("HW-7", result.HardwareId);
            Assert.True(result.IsManaged);
            Assert.Equal(MonitorBehaviorType.Dim, result.Behavior);
            Assert.Equal(25, result.DimLevel);
            Assert.True(result.LowerBrightnessOnBlackout);
            Assert.Equal(3, result.IdleValue);
            Assert.Equal(TimeUnit.Minutes, result.IdleUnit);
            Assert.True(result.IsActiveOnInput);
            Assert.False(result.IsActiveOnMousePosition);
            Assert.True(result.IsActiveOnActiveWindow);
            Assert.True(result.UseBrightnessSchedule);
            Assert.Equal(new[] { ("07:30", 90), ("22:00", 20) }, result.BrightnessSchedule.Select(e => (e.Start, e.Brightness)));
        }

        [Fact]
        public void Schedule_times_are_trimmed_on_the_way_out()
        {
            var stored = Managed();
            stored.BrightnessSchedule = [new() { Start = " 08:00 ", Brightness = 50 }];
            var sut = Panel();
            sut.ApplySettings(stored);

            Assert.Equal("08:00", sut.ToSettings().BrightnessSchedule.Single().Start);
        }

        [Fact]
        public void Lower_brightness_is_dropped_on_a_screen_without_ddc()
        {
            var sut = Panel(ddc: false);

            sut.ApplySettings(Managed());

            Assert.False(sut.LowerBrightnessOnBlackout);
        }

        [Fact]
        public void Applying_blackout_keeps_the_stored_dim_level()
        {
            var stored = Managed();
            stored.Behavior = MonitorBehaviorType.Blackout;
            stored.DimLevel = 40;
            var sut = Panel();

            sut.ApplySettings(stored);

            Assert.Equal(40, sut.DimLevel);
        }

        // ---- dirty tracking ----

        [Fact]
        public void Any_edit_makes_it_dirty_and_undoing_it_makes_it_clean()
        {
            var sut = Panel();

            sut.IdleValue = 99;
            Assert.True(sut.IsDirty);

            sut.IdleValue = new MonitorSettings().IdleValue;
            Assert.False(sut.IsDirty);
        }

        [Theory]
        [MemberData(nameof(Edits))]
        public void Every_setting_counts_as_an_edit(Action<MonitorConfigurationViewModel> edit)
        {
            var sut = Panel(ddc: true);

            edit(sut);

            Assert.True(sut.IsDirty);
        }

        public static TheoryData<Action<MonitorConfigurationViewModel>> Edits() => new()
        {
            p => p.IsManaged = true,
            p => p.Behavior = MonitorBehaviorType.Screensaver,
            p => p.DimLevel = 70,
            p => p.LowerBrightnessOnBlackout = true,
            p => p.IdleValue = 1,
            p => p.SelectedTimeUnit = TimeUnit.Hours,
            p => p.IsActiveOnInput = true,
            p => p.IsActiveOnMousePosition = false,
            p => p.IsActiveOnActiveWindow = true,
        };

        [Fact]
        public void Tiny_slider_noise_is_not_an_edit()
        {
            var sut = Panel();

            sut.DimLevel += 0.00001;

            Assert.False(sut.IsDirty);
        }

        [Fact]
        public void A_different_stored_schedule_is_an_edit()
        {
            var sut = Panel();
            var stored = new MonitorSettings { BrightnessSchedule = [new() { Start = "06:00", Brightness = 10 }] };

            sut.ApplySettings(stored);

            Assert.True(sut.IsDirty);
        }

        [Fact]
        public void Mark_as_saved_moves_the_baseline()
        {
            var sut = Panel();
            sut.DimLevel = 70;

            sut.MarkAsSaved();

            Assert.False(sut.IsDirty);
            Assert.Equal(70, sut.DimLevel);
        }

        [Fact]
        public void Revert_restores_the_saved_values_including_the_schedule()
        {
            var stored = Managed();
            stored.UseBrightnessSchedule = true;
            stored.BrightnessSchedule = [new() { Start = "09:00", Brightness = 60 }];
            var sut = Panel();
            sut.ApplySettings(stored);
            sut.MarkAsSaved();

            sut.ApplySettings(new MonitorSettings());
            sut.Revert();

            Assert.False(sut.IsDirty);
            Assert.True(sut.IsManaged);
            Assert.Equal(25, sut.DimLevel);
            Assert.Equal(MonitorBehaviorType.Dim, sut.Behavior);
            var schedule = sut.ToSettings();
            Assert.True(schedule.UseBrightnessSchedule);
            Assert.Equal("09:00", schedule.BrightnessSchedule.Single().Start);
        }

        [Fact]
        public void Dirty_flag_changes_are_announced()
        {
            var sut = Panel();
            var seen = new List<string?>();
            sut.PropertyChanged += (_, e) => seen.Add(e.PropertyName);

            sut.DimLevel = 80;

            Assert.Contains(nameof(MonitorConfigurationViewModel.IsDirty), seen);
        }

        // ---- behaviour options ----

        [Fact]
        public void Choosing_blackout_resets_the_dim_level()
        {
            var sut = Panel();
            sut.DimLevel = 60;

            sut.Behavior = MonitorBehaviorType.Blackout;

            Assert.Equal(0, sut.DimLevel);
        }

        [Fact]
        public void Slider_is_only_live_for_dimming()
        {
            var sut = Panel(ddc: true);

            sut.Behavior = MonitorBehaviorType.Dim;
            Assert.True(sut.IsDimSliderEnabled);

            sut.Behavior = MonitorBehaviorType.Flights;
            Assert.False(sut.IsDimSliderEnabled);
        }

        [Theory]
        [InlineData(true, MonitorBehaviorType.Blackout, true)]
        [InlineData(true, MonitorBehaviorType.Dim, false)]
        [InlineData(false, MonitorBehaviorType.Blackout, false)]
        public void Lower_brightness_needs_blackout_and_ddc(bool ddc, MonitorBehaviorType behavior, bool expected)
        {
            var sut = Panel(ddc);

            sut.Behavior = behavior;

            Assert.Equal(expected, sut.IsLowerBrightnessOptionEnabled);
            Assert.Equal(ddc, sut.IsDimBehaviorEnabled);
        }

        [Fact]
        public void Tooltips_explain_why_an_option_is_unavailable()
        {
            var noDdc = Panel(ddc: false);
            var ddc = Panel(ddc: true);
            ddc.Behavior = MonitorBehaviorType.Dim;

            const string noDdcNote = "(geen DDC/CI).";

            // The explanation always comes first; the reason for a grey option is a note at the end.
            Assert.StartsWith("Zwart zet de pixels", noDdc.BehaviorSectionTooltip);
            Assert.EndsWith(noDdcNote, noDdc.BehaviorSectionTooltip);
            Assert.StartsWith("Naast het zwart", noDdc.LowerBrightnessTooltip);
            Assert.EndsWith(noDdcNote, noDdc.LowerBrightnessTooltip);

            Assert.DoesNotContain("DDC/CI", ddc.BehaviorSectionTooltip);
            Assert.EndsWith("Kies eerst Zwart, dan kan deze aan.", ddc.LowerBrightnessTooltip);

            ddc.Behavior = MonitorBehaviorType.Blackout;
            Assert.DoesNotContain("\n\n", ddc.LowerBrightnessTooltip);
            Assert.EndsWith("achtergrondlicht anders nog door.", ddc.LowerBrightnessTooltip);
        }

        [Fact]
        public void Behaviour_change_refreshes_the_dependent_properties()
        {
            var sut = Panel(ddc: true);
            var seen = new List<string?>();
            sut.PropertyChanged += (_, e) => seen.Add(e.PropertyName);

            sut.Behavior = MonitorBehaviorType.Blackout;

            Assert.Contains(nameof(sut.IsDimSliderEnabled), seen);
            Assert.Contains(nameof(sut.IsLowerBrightnessOptionEnabled), seen);
            Assert.Contains(nameof(sut.LowerBrightnessTooltip), seen);
            Assert.Contains(nameof(sut.BehaviorError), seen);
        }

        [Fact]
        public void Time_units_offer_every_unit()
        {
            Assert.Equal(Enum.GetValues<TimeUnit>(), Panel().TimeUnits);
        }

        // ---- validation ----

        [Fact]
        public void An_unmanaged_screen_reports_no_field_errors()
        {
            var sut = Panel();
            sut.IdleValue = null;
            sut.IsActiveOnMousePosition = false;

            Assert.True(sut.IsValid);
            Assert.Equal(string.Empty, sut.IdleValueError);
            Assert.Equal(string.Empty, sut.ActiveConditionsError);
            Assert.Equal(string.Empty, sut.BehaviorError);
            Assert.Equal(string.Empty, sut.Error);
        }

        [Fact]
        public void A_correct_managed_screen_is_valid()
        {
            var sut = Panel(ddc: true);
            sut.ApplySettings(Managed());

            Assert.True(sut.IsValid);
            Assert.Equal(string.Empty, sut[nameof(sut.IdleValue)]);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(0)]
        [InlineData(-5)]
        public void Idle_time_must_be_positive(int? value)
        {
            var sut = Panel(ddc: true);
            sut.ApplySettings(Managed());

            sut.IdleValue = value;

            Assert.False(sut.IsValid);
            Assert.Equal("Vul een wachttijd groter dan 0 in.", sut.IdleValueError);
            Assert.Equal(sut.IdleValueError, sut[nameof(sut.IdleValue)]);
        }

        [Fact]
        public void A_behaviour_must_be_chosen()
        {
            var sut = Panel(ddc: true);
            sut.ApplySettings(Managed());

            sut.Behavior = MonitorBehaviorType.None;

            Assert.False(sut.IsValid);
            Assert.Equal("Kies wat er met dit scherm moet gebeuren.", sut.BehaviorError);
        }

        [Theory]
        [InlineData(nameof(MonitorConfigurationViewModel.IsActiveOnInput))]
        [InlineData(nameof(MonitorConfigurationViewModel.IsActiveOnMousePosition))]
        [InlineData(nameof(MonitorConfigurationViewModel.IsActiveOnActiveWindow))]
        public void At_least_one_wake_condition_is_needed(string column)
        {
            var sut = Panel(ddc: true);
            sut.ApplySettings(Managed());

            sut.IsActiveOnInput = false;
            sut.IsActiveOnMousePosition = false;
            sut.IsActiveOnActiveWindow = false;

            Assert.False(sut.IsValid);
            Assert.Equal("Kies minstens één 'Wakker houden als'-optie.", sut[column]);
            Assert.Equal(sut[column], sut.ActiveConditionsError);
        }

        [Fact]
        public void Unknown_columns_have_no_error()
        {
            var sut = Panel();
            sut.ApplySettings(Managed());

            Assert.Equal(string.Empty, sut["NoSuchProperty"]);
        }

        [Theory]
        [InlineData(new string[0], false)]
        [InlineData(new[] { "8 uur" }, false)]
        [InlineData(new[] { "08:00", "8:00" }, false)]
        [InlineData(new[] { "08:00", "19:30" }, true)]
        public void A_switched_on_stored_schedule_must_be_sensible(string[] times, bool valid)
        {
            var stored = Managed();
            stored.UseBrightnessSchedule = true;
            stored.BrightnessSchedule = times.Select(t => new BrightnessScheduleEntry { Start = t, Brightness = 50 }).ToList();
            var sut = Panel(ddc: true);

            sut.ApplySettings(stored);

            Assert.Equal(valid, sut.IsValid);
        }

        [Fact]
        public void A_switched_off_schedule_is_never_checked()
        {
            var stored = Managed();
            stored.UseBrightnessSchedule = false;
            stored.BrightnessSchedule = [];
            var sut = Panel(ddc: true);

            sut.ApplySettings(stored);

            Assert.True(sut.IsValid);
        }

        [Fact]
        public void Title_uses_the_shared_formatter()
        {
            var sut = Panel(number: 3);

            Assert.Equal("Scherm 3", sut.MonitorTitle);
        }

        // ---- helpers ----

        private static MonitorConfigurationViewModel Panel(bool ddc = false, string id = "HW-1", int number = 1) =>
            new(new MonitorInfo
            {
                HardwareId = id,
                DisplayNumber = number,
                Bounds = new Rect(0, 0, 1920, 1080),
                Capabilities = ddc ? new DdcCiCapabilities(true, 100) : null,
            });

        // A complete, valid managed configuration with every field away from its default.
        private static MonitorSettings Managed() => new()
        {
            IsManaged = true,
            Behavior = MonitorBehaviorType.Dim,
            DimLevel = 25,
            LowerBrightnessOnBlackout = true,
            IdleValue = 3,
            IdleUnit = TimeUnit.Minutes,
            IsActiveOnInput = true,
            IsActiveOnMousePosition = false,
            IsActiveOnActiveWindow = true,
        };
    }
}
