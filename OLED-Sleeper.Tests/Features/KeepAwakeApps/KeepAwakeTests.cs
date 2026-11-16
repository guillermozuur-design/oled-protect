using OLED_Sleeper.Features.KeepAwakeApps;
using System.Windows;

namespace OLED_Sleeper.Tests.Features.KeepAwakeApps
{
    public class KeepAwakeTests
    {
        private static readonly Rect Left = new(0, 0, 2560, 1440);
        private static readonly Rect Right = new(2560, 0, 2560, 1440);

        private static KeepAwakeRule Rule(string process, KeepAwakeScope scope) =>
            new() { Name = process, ProcessNames = new() { process }, Enabled = true, Scope = scope };

        [Fact]
        public void ThisScreen_KeepsOnlyTheScreenTheWindowIsOn()
        {
            var windows = new[] { new AppWindow("vlc", "film.mkv", new Rect(2700, 100, 1600, 900), false) };
            var (all, areas) = KeepAwakeEvaluator.Evaluate(windows, new[] { Rule("VLC", KeepAwakeScope.ThisScreen) });

            Assert.False(all);
            Assert.True(KeepAwakeEvaluator.KeepsScreenAwake(Right, areas));
            Assert.False(KeepAwakeEvaluator.KeepsScreenAwake(Left, areas));
        }

        [Fact]
        public void ThisScreen_AMinimisedWindowKeepsNothingAwake()
        {
            var windows = new[] { new AppWindow("vlc", "film.mkv", new Rect(2700, 100, 1600, 900), true) };
            var (all, areas) = KeepAwakeEvaluator.Evaluate(windows, new[] { Rule("vlc", KeepAwakeScope.ThisScreen) });

            Assert.False(all);
            Assert.Empty(areas);
        }

        [Fact]
        public void ThisScreen_AWindowThatOnlyPeeksOverTheEdgeDoesNotCount()
        {
            // 100 px of a 1600 px wide window sticks out onto the left screen: less than a fifth.
            var windows = new[] { new AppWindow("vlc", "film", new Rect(2460, 100, 1600, 900), false) };
            var (_, areas) = KeepAwakeEvaluator.Evaluate(windows, new[] { Rule("vlc", KeepAwakeScope.ThisScreen) });

            Assert.False(KeepAwakeEvaluator.KeepsScreenAwake(Left, areas));
            Assert.True(KeepAwakeEvaluator.KeepsScreenAwake(Right, areas));
        }

        [Fact]
        public void AllScreens_EvenWhenMinimised()
        {
            var windows = new[] { new AppWindow("obs64", "OBS", new Rect(0, 0, 800, 600), true) };
            var (all, _) = KeepAwakeEvaluator.Evaluate(windows, new[] { Rule("obs64", KeepAwakeScope.AllScreens) });

            Assert.True(all);
        }

        [Fact]
        public void OtherApps_DoNothing()
        {
            var windows = new[] { new AppWindow("notepad", "x", Right, false) };
            var (all, areas) = KeepAwakeEvaluator.Evaluate(windows, new[] { Rule("vlc", KeepAwakeScope.ThisScreen) });

            Assert.False(all);
            Assert.Empty(areas);
        }

        [Fact]
        public void Settings_AlwaysContainTheReadyMadeApps_OffByDefault()
        {
            var settings = new KeepAwakeAppsSettings();

            Assert.Equal(KeepAwakeAppsSettings.Presets.Count, settings.Rules.Count);
            Assert.All(settings.Rules, r => Assert.False(r.Enabled));
            Assert.Contains(settings.Rules, r => r.Name == "VLC");
            Assert.Empty(settings.ActiveRules);
        }

        [Fact]
        public void Settings_KeepChoicesAndOwnApps_WithoutDuplicates()
        {
            var settings = new KeepAwakeAppsSettings();
            var rules = settings.Rules.ToList();
            rules.First(r => r.Name == "VLC").Enabled = true;
            rules.Add(new KeepAwakeRule { Name = "game", ProcessNames = new() { "game" }, Enabled = true });
            rules.Add(new KeepAwakeRule { Name = "vlc again", ProcessNames = new() { "VLC" }, Enabled = true }); // duplicate of a preset

            settings.SetRules(rules);

            Assert.Equal(KeepAwakeAppsSettings.Presets.Count + 1, settings.Rules.Count);
            Assert.Equal(new[] { "VLC", "game" }, settings.ActiveRules.Select(r => r.Name));
        }
    }
}
