using MirahelpBLEToolkit.Core.Enums;
using System;
using System.Collections.Generic;

namespace MirahelpBLEToolkit.Core.Events
{
    public sealed class AdvertisementReceivedEventArgs
    {
        public UInt64 Address { get; set; }
        public String Name { get; set; } = String.Empty;
        public Int16 Rssi { get; set; }
        public DateTime TimestampUtc { get; set; }
        public String Manufacturer { get; set; } = String.Empty;
        public IReadOnlyList<Guid> ServiceUuids { get; set; } = new List<Guid>();
        public AdvertisementTypeOptions AdvertisementType { get; set; }
    }
}