using System;

namespace MirahelpBLEToolkit.Core.Models
{
    public sealed class RssiSample
    {
        public DateTime TimestampUtc { get; set; }
        public Int16 Rssi { get; set; }
    }
}