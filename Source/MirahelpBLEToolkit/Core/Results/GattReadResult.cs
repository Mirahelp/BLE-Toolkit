using MirahelpBLEToolkit.Core.Enums;
using System;

namespace MirahelpBLEToolkit.Core.Results
{
    public sealed class GattReadResult
    {
        public GattCommunicationStatusOptions Status { get; set; }
        public Byte[] Data { get; set; } = Array.Empty<Byte>();
    }
}