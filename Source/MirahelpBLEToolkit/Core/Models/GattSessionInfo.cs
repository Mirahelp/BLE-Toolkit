using System;

namespace MirahelpBLEToolkit.Core.Models
{
    public sealed class GattSessionInfo
    {
        public Int32 MaxPdu { get; set; }
        public Boolean MaintainConnection { get; set; }
        public String SessionStatus { get; set; } = String.Empty;
    }
}