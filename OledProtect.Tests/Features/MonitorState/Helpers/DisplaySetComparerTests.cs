using OledProtect.Features.MonitorInformation.Models;
using OledProtect.Features.MonitorState.Helpers;
using System.Windows;

namespace OledProtect.Tests.Features.MonitorState.Helpers
{
    public class DisplaySetComparerTests
    {
        private const string Lg = @"MONITOR\GSM5C7C\{4d36e96e-e325-11ce-bfc1-08002be10318}\0002";
        private const string Acer = @"MONITOR\ACR074D\{4d36e96e-e325-11ce-bfc1-08002be10318}\0003";
        private const string Unresolved = "";

        // Builds a 2560x1440 screen; the display number follows the device name's last digit.
        private static MonitorInfo At(int display, string id, double x = 0, bool primary = false, uint scale = 96, string topology = "")
            => new()
            {
                DeviceName = $@"\\.\DISPLAY{display}",
                DisplayNumber = display,
                HardwareId = id,
                Bounds = new Rect(x, 0, 2560, 1440),
                IsPrimary = primary,
                Dpi = scale,
                TopologyLabel = topology
            };

        private static bool Same(MonitorInfo[] a, MonitorInfo[] b) => DisplaySetComparer.AreEquivalent(a, b);

        [Fact]
        public void IdenticalSets_AreTheSame() =>
            Assert.True(Same([At(1, Lg), At(2, Acer, 2560)], [At(1, Lg), At(2, Acer, 2560)]));

        [Fact]
        public void Order_DoesNotMatter() =>
            Assert.True(Same([At(1, Lg), At(2, Acer, 2560)], [At(2, Acer, 2560), At(1, Lg)]));

        [Fact]
        public void EmptySets_AreTheSame() =>
            Assert.True(Same([], []));

        [Fact]
        public void AMissingReading_IsNeverTheSame()
        {
            IReadOnlyList<MonitorInfo> one = [At(1, Lg)];

            Assert.False(DisplaySetComparer.AreEquivalent(null, one));
            Assert.False(DisplaySetComparer.AreEquivalent(one, null));
            Assert.False(DisplaySetComparer.AreEquivalent(null, null));
        }

        [Fact]
        public void AnUnpluggedScreen_IsAChange() =>
            Assert.False(Same([At(1, Lg), At(2, Acer, 2560)], [At(1, Lg)]));

        [Fact]
        public void PanelsSwappingDeviceNames_IsAChange()
        {
            MonitorInfo lgAsTwo = At(2, Lg);
            MonitorInfo acerAsOne = At(1, Acer, 2560);

            Assert.False(Same([At(1, Lg), At(2, Acer, 2560)], [lgAsTwo, acerAsOne]));
        }

        [Fact]
        public void ADifferentPanelUnderTheSameName_IsAChange() =>
            Assert.False(Same([At(1, Lg)], [At(1, Acer)]));

        [Theory]
        [InlineData(-2560, false, 96u, "")]   // moved
        [InlineData(0, true, 96u, "")]        // became primary
        [InlineData(0, false, 120u, "")]      // scaling changed
        [InlineData(0, false, 96u, "Extend")] // topology changed
        public void AnyPresentationDifference_IsAChange(double x, bool primary, uint scale, string topology) =>
            Assert.False(Same([At(1, Lg)], [At(1, Lg, x, primary, scale, topology)]));

        [Fact]
        public void AHardwareIdThatFailedToResolve_IsMatchedByName() =>
            Assert.True(Same([At(1, Lg), At(2, Acer, 2560)], [At(1, Lg), At(2, Unresolved, 2560)]));

        [Fact]
        public void AHardwareIdThatResolvesAgain_IsMatchedByName() =>
            Assert.True(Same([At(1, Unresolved)], [At(1, Lg)]));

        [Fact]
        public void AnUnresolvedScreenThatMoved_IsAChange() =>
            Assert.False(Same([At(1, Unresolved)], [At(1, Unresolved, -2560)]));

        [Fact]
        public void TwoResolvedButDifferentIds_AreNotMatchedByName() =>
            Assert.False(Same([At(1, Lg), At(2, Unresolved, 2560)], [At(1, Acer), At(2, Unresolved, 2560)]));
    }
}
