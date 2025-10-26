using System;

namespace MirahelpBLEToolkit.Core.Results
{
    public sealed class HeartbeatProbeResult
    {
        public UInt64 Address { get; set; }
        public Boolean Succeeded { get; set; }
        public Int32 LatencyMs { get; set; }
        public DateTime TimestampUtc { get; set; }
        public String Error { get; set; } = String.Empty;
    }
}