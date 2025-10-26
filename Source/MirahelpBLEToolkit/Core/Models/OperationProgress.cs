using System;

namespace MirahelpBLEToolkit.Core.Models
{
    public sealed class OperationProgress
    {
        public Guid OperationId { get; set; }
        public String Scope { get; set; } = String.Empty;
        public String Text { get; set; } = String.Empty;
        public Boolean Visible { get; set; }
        public Boolean Active { get; set; }
    }
}