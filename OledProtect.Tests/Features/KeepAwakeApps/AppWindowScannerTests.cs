using OledProtect.Features.KeepAwakeApps;
using Xunit.Abstractions;

namespace OledProtect.Tests.Features.KeepAwakeApps
{
    public class AppWindowScannerTests
    {
        private readonly ITestOutputHelper _output;

        public AppWindowScannerTests(ITestOutputHelper output) => _output = output;

        /// <summary>Reads the real desktop. Must never throw; on a build server there may be no windows at all.</summary>
        [Fact]
        public void Scan_ReadsTheOpenWindowsWithoutThrowing()
        {
            var windows = AppWindowScanner.Scan();
            foreach (var group in windows.GroupBy(w => w.ProcessName))
                _output.WriteLine($"{group.Key}: {group.Count()} venster(s), bv. '{group.First().Title}' {group.First().Bounds}");
            Assert.All(windows, w => Assert.False(string.IsNullOrEmpty(w.ProcessName)));
        }
    }
}
