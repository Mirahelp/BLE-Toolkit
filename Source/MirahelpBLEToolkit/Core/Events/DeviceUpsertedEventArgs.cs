using MirahelpBLEToolkit.Core.Models;
using System;

namespace MirahelpBLEToolkit.Core.Events
{
    public sealed class DeviceUpsertedEventArgs
    {
        public DeviceState Device { get; set; } = new DeviceState();
        public Boolean IsNew { get; set; }
    }
}