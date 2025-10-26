using MirahelpBLEToolkit.Core.Enums;
using System;

namespace MirahelpBLEToolkit.Core.Models
{
    public sealed class MessageRecord
    {
        public Int64 Id { get; set; }
        public DateTime TimestampUtc { get; set; }
        public UInt64 Address { get; set; }
        public MessageDirectionOptions Direction { get; set; }
        public MessageKindOptions Kind { get; set; }
        public Guid? Service { get; set; }
        public Guid? Characteristic { get; set; }
        public Byte[] Data { get; set; } = Array.Empty<Byte>();
        public String Text { get; set; } = String.Empty;
    }
}