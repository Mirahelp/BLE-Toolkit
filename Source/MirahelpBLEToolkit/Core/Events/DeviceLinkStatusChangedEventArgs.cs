using MirahelpBLEToolkit.Core.Enums;
using System;

namespace MirahelpBLEToolkit.Core.Events
{
    public sealed class DeviceLinkStatusChangedEventArgs
    {
        public UInt64 Address { get; set; }
        public ConnectionStatusOptions Status { get; set; }
        public DateTime TimestampUtc { get; set; }
    }
}