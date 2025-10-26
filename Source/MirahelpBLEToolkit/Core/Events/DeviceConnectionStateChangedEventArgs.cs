using MirahelpBLEToolkit.Core.Models;

namespace MirahelpBLEToolkit.Core.Events
{
    public sealed class DeviceConnectionStateChangedEventArgs
    {
        public DeviceConnectionSnapshot Snapshot { get; set; } = new DeviceConnectionSnapshot();
    }
}