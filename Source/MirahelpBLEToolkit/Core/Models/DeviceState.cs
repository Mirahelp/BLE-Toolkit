using MirahelpBLEToolkit.Core.Enums;
using System;
using System.Collections.Generic;

namespace MirahelpBLEToolkit.Core.Models
{
    public sealed class DeviceState
    {
        public String DeviceId { get; set; } = String.Empty;
        public UInt64 Address { get; set; }
        public String Name { get; set; } = String.Empty;
        public Boolean IsPaired { get; set; }
        public DateTime FirstSeenUtc { get; set; }
        public DateTime LastSeenUtc { get; set; }
        public Int16? LastRssi { get; set; }
        public Boolean Pinned { get; set; }
        public ConnectionStatusOptions ConnectionStatus { get; set; }
        public Int32 Number { get; set; }
        public Int32 Ordinal { get; set; }
        public String Manufacturer { get; set; } = String.Empty;
        public IReadOnlyList<Guid> AdvertisedServiceUuids { get; set; } = new List<Guid>();
        public AdvertisementTypeOptions AdvertisementType { get; set; }
    }
}