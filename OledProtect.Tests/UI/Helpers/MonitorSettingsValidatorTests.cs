using OledProtect.Features.MonitorBehavior.Models;
using OledProtect.UI.Helpers;
using OledProtect.UI.ViewModels;
using static OledProtect.Tests.UI.Helpers.ScreenTileBuilder;

namespace OledProtect.Tests.UI.Helpers
{
    public class MonitorSettingsValidatorTests
    {
        private const string NoBehavior = "Kies wat er met dit scherm moet gebeuren.";
        private const string NoIdleTime = "Vul een wachttijd groter dan 0 in.";
        private const string NoWakeReason = "Kies minstens één 'Wakker houden als'-optie.";

        private static string? Check(params MonitorLayoutViewModel[] tiles) =>
            MonitorSettingsValidator.BuildValidationError(tiles);

        [Fact]
        public void An_empty_screen_list_is_fine()
        {
            Assert.Null(Check());
        }

        [Fact]
        public void Complete_watched_screens_raise_no_message()
        {
            Assert.Null(Check(Screen(1).ReadyToSave(), Screen(2).ReadyToSave(), Screen(3).ReadyToSave()));
        }

        // Ways to make an otherwise complete screen invalid, by name so the theories stay serialisable.
        private static readonly Dictionary<string, Action<MonitorConfigurationViewModel>> Breakages = new()
        {
            ["idle zero"] = c => c.IdleValue = 0,
            ["idle negative"] = c => c.IdleValue = -3,
            ["idle empty"] = c => c.IdleValue = null,
            ["no behaviour"] = c => c.Behavior = MonitorBehaviorType.None,
            ["no wake reason"] = c =>
            {
                c.IsActiveOnInput = false;
                c.IsActiveOnMousePosition = false;
                c.IsActiveOnActiveWindow = false;
            },
        };

        [Theory]
        [InlineData("idle zero")]
        [InlineData("idle empty")]
        [InlineData("no behaviour")]
        [InlineData("no wake reason")]
        public void A_screen_that_is_not_watched_is_never_judged(string breakage)
        {
            var tile = Screen(1).ReadyToSave().Then(Breakages[breakage]).Then(c => c.IsManaged = false);

            Assert.Null(Check(tile));
        }

        [Theory]
        [InlineData("no behaviour", NoBehavior)]
        [InlineData("idle negative", NoIdleTime)]
        [InlineData("no wake reason", NoWakeReason)]
        public void Each_problem_is_reported_on_its_own(string breakage, string expected)
        {
            string report = Check(Screen(4).ReadyToSave().Then(Breakages[breakage])) ?? "";

            Assert.Contains("Scherm 4", report);
            foreach (var message in new[] { NoBehavior, NoIdleTime, NoWakeReason })
                Assert.Equal(message == expected, report.Contains(message));
        }

        [Fact]
        public void A_fresh_watched_screen_collects_all_three_problems()
        {
            var fresh = Screen(2).Watched().Then(c =>
            {
                c.IdleValue = null;
                c.IsActiveOnMousePosition = false;
            });

            string report = Check(fresh) ?? "";

            Assert.All(new[] { "Scherm 2", NoBehavior, NoIdleTime, NoWakeReason }, part => Assert.Contains(part, report));
        }

        [Fact]
        public void Only_faulty_screens_are_named_and_in_the_given_order()
        {
            string report = Check(
                Screen(3).Watched(),
                Screen(2).ReadyToSave(),
                Screen(1).Watched()) ?? "";

            int third = report.IndexOf("Scherm 3", StringComparison.Ordinal);
            int first = report.IndexOf("Scherm 1", StringComparison.Ordinal);
            Assert.InRange(third, 0, first - 1);
            Assert.DoesNotContain("Scherm 2", report);
        }

        [Fact]
        public void The_report_opens_with_the_reason_and_closes_with_the_way_out()
        {
            string report = Check(Screen(1).Watched())!;
            string[] lines = report.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

            Assert.StartsWith("Een of meer schermen zijn nog niet goed ingesteld", lines[0]);
            Assert.EndsWith("zet het bewaken van dat scherm uit.", lines[^1]);
        }
    }
}
