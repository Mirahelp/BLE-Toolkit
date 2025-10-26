using MirahelpBLEToolkit.Core.Enums;
using System;

namespace MirahelpBLEToolkit.Core.Models
{
    public sealed class MessageQuery
    {
        public UInt64 Address { get; set; }
        public MessageDirectionOptions? Direction { get; set; }
        public MessageKindOptions[] Kinds { get; set; } = Array.Empty<MessageKindOptions>();
        public Guid? Service { get; set; }
        public Guid? Characteristic { get; set; }
        public DateTime? SinceUtc { get; set; }
        public Int32 Limit { get; set; } = 1000;
    }
}