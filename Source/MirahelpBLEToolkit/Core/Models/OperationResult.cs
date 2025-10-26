using System;

namespace MirahelpBLEToolkit.Core.Models
{
    public sealed class OperationResult
    {
        public Boolean Succeeded { get; set; }
        public String Message { get; set; } = String.Empty;
        public Exception? Exception { get; set; }
        public TimeSpan Duration { get; set; }
    }
}