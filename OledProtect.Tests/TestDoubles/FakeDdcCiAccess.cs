using OledProtect.Features.MonitorDimming.Services.Interfaces;

namespace OledProtect.Tests.TestDoubles
{
    /// <summary>
    /// Hands out <see cref="FakeDdcCiSession"/> panels by device name. A name without a panel is unreachable.
    /// Each name always gets the same panel object back, so its level carries over between opens and its
    /// disposals add up.
    /// </summary>
    public sealed class FakeDdcCiAccess : IDdcCiAccess
    {
        private readonly Dictionary<string, FakeDdcCiSession> _panelsByDevice = [];

        /// <summary>Every device name an open was asked for, reachable or not, oldest first.</summary>
        public List<string> OpenRequests { get; } = [];

        /// <summary>
        /// Called at the start of each open with the device name. Blocking in here keeps the caller inside the
        /// monitor's lock, which is how the tests hold an operation in flight.
        /// </summary>
        public Action<string>? WhileOpening { get; set; }

        /// <summary>Makes <paramref name="deviceName"/> reachable through a new panel at <paramref name="brightness"/>.</summary>
        public FakeDdcCiSession AddPanel(string deviceName, uint brightness) =>
            _panelsByDevice[deviceName] = new FakeDdcCiSession(brightness);

        public IDdcCiSession? OpenSession(string gdiDeviceName)
        {
            OpenRequests.Add(gdiDeviceName);
            WhileOpening?.Invoke(gdiDeviceName);
            return _panelsByDevice.GetValueOrDefault(gdiDeviceName);
        }
    }
}
