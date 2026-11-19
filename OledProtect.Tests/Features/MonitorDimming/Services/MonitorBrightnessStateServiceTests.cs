using OledProtect.Features.MonitorDimming.Services;
using OledProtect.Storage.Interfaces;

namespace OledProtect.Tests.Features.MonitorDimming.Services
{
    public sealed class MonitorBrightnessStateServiceTests
    {
        // Installed copies already keep their saved levels in this file, so its name is part of the contract.
        private const string StateFile = "brightness_state.json";

        private readonly MemoryFiles _files = new();

        [Fact]
        public void Saved_levels_come_back_as_they_were_written()
        {
            _files.Content[StateFile] = new Dictionary<string, uint> { ["GSM-1"] = 80, ["DEL-2"] = 120 };

            var loaded = new MonitorBrightnessStateService(_files).LoadState();

            Assert.Equal(new Dictionary<string, uint> { ["GSM-1"] = 80, ["DEL-2"] = 120 }, loaded);
        }

        [Fact]
        public void Missing_or_unreadable_file_loads_as_an_empty_map()
        {
            var loaded = new MonitorBrightnessStateService(_files).LoadState();

            Assert.NotNull(loaded);
            Assert.Empty(loaded);
        }

        [Fact]
        public void Saving_writes_the_map_to_the_state_file()
        {
            var levels = new Dictionary<string, uint> { ["GSM-1"] = 80 };

            new MonitorBrightnessStateService(_files).SaveState(levels);

            Assert.Equal([StateFile], _files.Content.Keys);
            Assert.Same(levels, _files.Content[StateFile]);
        }

        private sealed class MemoryFiles : IAppDataFileStore
        {
            public Dictionary<string, object?> Content { get; } = [];

            public T? Read<T>(string fileName) => Content.TryGetValue(fileName, out var value) ? (T?)value : default;

            public bool TryWrite<T>(string fileName, T value)
            {
                Content[fileName] = value;
                return true;
            }
        }
    }
}
