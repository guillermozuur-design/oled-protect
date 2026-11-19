using Moq;
using OLED_Sleeper.Features.MonitorDimming.Services;
using OLED_Sleeper.Features.MonitorDimming.Services.Interfaces;

namespace OLED_Sleeper.Tests.Features.MonitorDimming.Services
{
    public sealed class OriginalBrightnessStoreTests
    {
        private const string Panel = "PNP-OLED";

        private readonly Mock<IMonitorBrightnessStateService> _file = new();

        public OriginalBrightnessStoreTests() => FileHolds(new Dictionary<string, uint>());

        [Fact]
        public void Recordings_SavedByAnEarlierRun_AreAvailableRightAway()
        {
            FileHolds(new Dictionary<string, uint> { [Panel] = 80 });

            var store = NewStore();

            Assert.True(store.TryGetOriginal(Panel, out uint level));
            Assert.Equal(80u, level);
        }

        [Fact]
        public void TryGetOriginal_UnknownMonitor_IsFalse()
        {
            Assert.False(NewStore().TryGetOriginal(Panel, out _));
        }

        [Fact]
        public void RecordOriginal_FirstTime_KeepsTheLevelAndWritesItToTheFile()
        {
            var store = NewStore();

            store.RecordOriginal(Panel, 80);

            Assert.True(store.TryGetOriginal(Panel, out uint level));
            Assert.Equal(80u, level);
            _file.Verify(f => f.SaveState(It.Is<Dictionary<string, uint>>(saved => saved[Panel] == 80)), Times.Once);
        }

        [Fact]
        public void RecordOriginal_AlreadyRecorded_KeepsTheFirstLevelAndSavesNothingMore()
        {
            var store = NewStore();
            store.RecordOriginal(Panel, 80);

            store.RecordOriginal(Panel, 5);

            store.TryGetOriginal(Panel, out uint level);
            Assert.Equal(80u, level);
            _file.Verify(f => f.SaveState(It.IsAny<Dictionary<string, uint>>()), Times.Once);
        }

        [Fact]
        public void ReplaceOriginal_AlreadyRecorded_OverwritesAndSaves()
        {
            var store = NewStore();
            store.RecordOriginal(Panel, 80);

            store.ReplaceOriginal(Panel, 40);

            store.TryGetOriginal(Panel, out uint level);
            Assert.Equal(40u, level);
            _file.Verify(f => f.SaveState(It.Is<Dictionary<string, uint>>(saved => saved[Panel] == 40)), Times.Once);
        }

        [Fact]
        public void RemoveOriginal_Recorded_ForgetsItAndSavesTheEmptyMap()
        {
            var store = NewStore();
            store.RecordOriginal(Panel, 80);

            store.RemoveOriginal(Panel);

            Assert.False(store.TryGetOriginal(Panel, out _));
            _file.Verify(f => f.SaveState(It.Is<Dictionary<string, uint>>(saved => saved.Count == 0)), Times.Once);
        }

        [Fact]
        public void RemoveOriginal_NothingRecorded_DoesNotTouchTheFile()
        {
            NewStore().RemoveOriginal(Panel);

            _file.Verify(f => f.SaveState(It.IsAny<Dictionary<string, uint>>()), Times.Never);
        }

        [Fact]
        public void GetAll_ReturnsACopyThatCannotChangeTheStore()
        {
            var store = NewStore();
            store.RecordOriginal(Panel, 80);

            var copy = store.GetAll();
            copy.Clear();

            Assert.True(store.TryGetOriginal(Panel, out _));
        }

        private OriginalBrightnessStore NewStore() => new(_file.Object);

        private void FileHolds(Dictionary<string, uint> content) => _file.Setup(f => f.LoadState()).Returns(content);
    }
}
