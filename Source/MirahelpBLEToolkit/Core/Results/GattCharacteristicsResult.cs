using MirahelpBLEToolkit.Core.Enums;
using MirahelpBLEToolkit.Core.Interfaces;
using System.Collections.Generic;

namespace MirahelpBLEToolkit.Core.Results
{
    public sealed class GattCharacteristicsResult
    {
        public IReadOnlyList<IGattCharacteristicService> Characteristics { get; set; } = new List<IGattCharacteristicService>();
        public GattCommunicationStatusOptions Status { get; set; }
    }
}