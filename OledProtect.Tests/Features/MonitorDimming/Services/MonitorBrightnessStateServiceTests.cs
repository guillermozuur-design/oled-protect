using Moq;
using OledProtect.Features.MonitorDimming.Services;
using OledProtect.Storage.Interfaces;

namespace OledProtect.Tests.Features.MonitorDimming.Services
{
    public sealed class MonitorBrightnessStateServiceTests
    {
        // Existing installs have their state under this name; the test pins it so a rename cannot slip through.
        private const string ExpectedFile = "brightness_state.json";

        private readonly Mock<IAppDataFileStore> _store = new();

        [Fact]
        public void LoadState_ReturnsWhatTheFileHolds()
        {
            _store.Setup(s => s.Read<Dictionary<string, uint>>(ExpectedFile))
                .Returns(new Dictionary<string, uint> { ["PNP-OLED"] = 80 });

            var loaded = new MonitorBrightnessStateService(_store.Object).LoadState();

            Assert.Equal(80u, Assert.Single(loaded).Value);
        }

        [Fact]
        public void LoadState_UnreadableOrMissingFile_GivesAnEmptyMap()
        {
            _store.Setup(s => s.Read<Dictionary<string, uint>>(ExpectedFile)).Returns(default(Dictionary<string, uint>));

            var loaded = new MonitorBrightnessStateService(_store.Object).LoadState();

            Assert.NotNull(loaded);
            Assert.Empty(loaded);
        }

        [Fact]
        public void SaveState_WritesTheMapToTheStateFile()
        {
            var snapshot = new Dictionary<string, uint> { ["PNP-OLED"] = 80 };

            new MonitorBrightnessStateService(_store.Object).SaveState(snapshot);

            _store.Verify(s => s.TryWrite(ExpectedFile, snapshot), Times.Once);
        }
    }
}
