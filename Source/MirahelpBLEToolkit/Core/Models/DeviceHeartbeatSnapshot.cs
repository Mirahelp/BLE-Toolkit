using System;

namespace MirahelpBLEToolkit.Core.Models
{
    public sealed class DeviceHeartbeatSnapshot
    {
        public UInt64 Address { get; set; }
        public Boolean Enabled { get; set; }
        public Boolean IsProbing { get; set; }
        public DateTime LastSuccessUtc { get; set; }
        public DateTime LastAttemptUtc { get; set; }
        public DateTime LastFailureUtc { get; set; }
        public Int32 LastLatencyMs { get; set; }
        public Int32 ConsecutiveFailures { get; set; }
        public String LastError { get; set; } = String.Empty;
        public DateTime NextPlannedProbeUtc { get; set; }
    }
}