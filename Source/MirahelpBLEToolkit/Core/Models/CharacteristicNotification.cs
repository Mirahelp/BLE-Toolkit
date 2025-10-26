using System;

namespace MirahelpBLEToolkit.Core.Models
{
    public sealed class CharacteristicNotification
    {
        public UInt64 Address { get; set; }
        public Guid Service { get; set; }
        public Guid Characteristic { get; set; }
        public DateTime TimestampUtc { get; set; }
        public Byte[] Data { get; set; } = Array.Empty<Byte>();
    }
}