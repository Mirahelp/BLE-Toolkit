using MirahelpBLEToolkit.Core.Enums;
using MirahelpBLEToolkit.Core.Interfaces;
using System.Collections.Generic;

namespace MirahelpBLEToolkit.Core.Results
{
    public sealed class GattServicesResult
    {
        public IReadOnlyList<IGattServiceService> Services { get; set; } = new List<IGattServiceService>();
        public GattCommunicationStatusOptions Status { get; set; }
        public IBleDeviceService? Device { get; set; }
    }
}
