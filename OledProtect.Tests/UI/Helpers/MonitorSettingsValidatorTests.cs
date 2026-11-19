using OledProtect.UI.Helpers;
using OledProtect.UI.ViewModels;

namespace OledProtect.Tests.UI.Helpers
{
    public class MonitorSettingsValidatorTests
    {
        [Fact]
        public void No_monitors_is_nothing_to_complain_about()
        {
            Assert.Null(MonitorSettingsValidator.BuildValidationError(Array.Empty<MonitorLayoutViewModel>()));
        }

        [Fact]
        public void Valid_managed_monitors_pass()
        {
            var tiles = new[] { TestMonitors.ValidManagedTile(1), TestMonitors.ValidManagedTile(2) };

            Assert.Null(MonitorSettingsValidator.BuildValidationError(tiles));
        }

        [Fact]
        public void Unmanaged_monitors_are_not_checked()
        {
            var tile = TestMonitors.Tile(1);
            tile.Configuration.IdleValue = 0;

            Assert.Null(MonitorSettingsValidator.BuildValidationError(new[] { tile }));
        }

        [Fact]
        public void A_broken_managed_monitor_is_named_with_each_of_its_problems()
        {
            var tile = TestMonitors.Tile(2);
            tile.Configuration.IsManaged = true;
            tile.Configuration.IdleValue = null;
            tile.Configuration.IsActiveOnMousePosition = false;

            string? message = MonitorSettingsValidator.BuildValidationError(new[] { tile });

            Assert.NotNull(message);
            Assert.Contains("Scherm 2", message);
            Assert.Contains("Kies wat er met dit scherm moet gebeuren.", message);
            Assert.Contains("Vul een wachttijd groter dan 0 in.", message);
            Assert.Contains("Kies minstens één 'Wakker houden als'-optie.", message);
        }

        [Fact]
        public void Only_the_broken_monitors_are_listed()
        {
            var brokenA = TestMonitors.Tile(1);
            var brokenB = TestMonitors.Tile(2);
            brokenA.Configuration.IsManaged = true;
            brokenB.Configuration.IsManaged = true;

            string? message = MonitorSettingsValidator.BuildValidationError(
                new[] { brokenA, brokenB, TestMonitors.ValidManagedTile(3) });

            Assert.NotNull(message);
            Assert.Contains("Scherm 1", message);
            Assert.Contains("Scherm 2", message);
            Assert.DoesNotContain("Scherm 3", message);
        }

        [Fact]
        public void The_message_explains_the_problem_and_how_to_fix_it()
        {
            var tile = TestMonitors.Tile(1);
            tile.Configuration.IsManaged = true;

            string message = MonitorSettingsValidator.BuildValidationError(new[] { tile })!;

            Assert.StartsWith("Een of meer schermen zijn nog niet goed ingesteld", message);
            Assert.Contains("zet het bewaken van dat scherm uit", message.TrimEnd());
        }
    }
}
