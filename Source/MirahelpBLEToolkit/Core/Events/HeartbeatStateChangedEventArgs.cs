using MirahelpBLEToolkit.Core.Models;

namespace MirahelpBLEToolkit.Core.Events
{
    public sealed class HeartbeatStateChangedEventArgs
    {
        public DeviceHeartbeatSnapshot Snapshot { get; set; } = new DeviceHeartbeatSnapshot();
    }
}