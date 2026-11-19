using OledProtect.Features.TaskbarDim;
using System.Windows;

namespace OledProtect.Tests.Features.TaskbarDim
{
    public class TaskbarDimTests
    {
        private static readonly Rect Monitor = new(0, 0, 2560, 1440);
        private static readonly Rect Bottom = new(0, 1392, 2560, 48);

        [Fact]
        public void DimsOnlyAfterFiveSecondsWithoutTheMouse()
        {
            var t = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            Assert.False(TaskbarDimRules.ShouldDim(t, t.AddSeconds(4.9)));
            Assert.True(TaskbarDimRules.ShouldDim(t, t.AddSeconds(5)));
        }

        [Fact]
        public void MouseNearTheTaskbarCounts()
        {
            Assert.True(TaskbarDimRules.IsNear(Bottom, new Point(1000, 1420)));  // on it
            Assert.True(TaskbarDimRules.IsNear(Bottom, new Point(1000, 1375)));  // just above it
            Assert.False(TaskbarDimRules.IsNear(Bottom, new Point(1000, 900)));  // far away
        }

        [Fact]
        public void HiddenTaskbarIsNotDimmed()
        {
            Assert.True(TaskbarDimRules.IsShown(Bottom, Monitor));
            Assert.False(TaskbarDimRules.IsShown(new Rect(0, 1438, 2560, 48), Monitor)); // auto-hide: a 2 px sliver
        }

        private sealed class MemoryStore : OledProtect.Storage.Interfaces.IAppDataFileStore
        {
            public readonly Dictionary<string, object> Files = new();
            public T? Read<T>(string fileName) => Files.TryGetValue(fileName, out var v) ? (T)v : default;
            public bool TryWrite<T>(string fileName, T value) { Files[fileName] = value!; return true; }
        }

        [Fact]
        public void Setting_OffByDefault_SavedAndAnnounced()
        {
            var store = new MemoryStore();
            var s = new TaskbarDimSettings(store);
            int changes = 0;
            s.Changed += () => changes++;
            Assert.False(s.Enabled);
            s.SetEnabled(true);
            s.SetEnabled(true);
            Assert.Equal(1, changes);
            Assert.True(new TaskbarDimSettings(store).Enabled);
        }
    }
}
