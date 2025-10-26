using MirahelpBLEToolkit.Core.Enums;
using System;

namespace MirahelpBLEToolkit.Core.Models
{
    public sealed class DeviceConnectionSnapshot
    {
        public UInt64 Address { get; set; }
        public DeviceLinkStateOptions State { get; set; }
        public Guid AttemptId { get; set; }
        public Int64 Sequence { get; set; }
        public Int32 BusyDepth { get; set; }
        public Boolean AutoReconnectEnabled { get; set; }
        public DateTime ConnectedSinceUtc { get; set; }
        public DateTime NextReconnectUtc { get; set; }
        public String LastError { get; set; } = String.Empty;
    }
}