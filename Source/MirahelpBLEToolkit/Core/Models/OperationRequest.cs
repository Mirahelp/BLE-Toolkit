using System;

namespace MirahelpBLEToolkit.Core.Models
{
    public sealed class OperationRequest
    {
        public String Scope { get; set; } = String.Empty;
        public String Title { get; set; } = String.Empty;
        public UInt64 Address { get; set; }
        public DateTime EnqueuedUtc { get; set; }
        public TimeSpan Timeout { get; set; }
    }
}