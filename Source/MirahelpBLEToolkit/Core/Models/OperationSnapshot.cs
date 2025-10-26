using System.Collections.Generic;

namespace MirahelpBLEToolkit.Core.Models
{
    public sealed class OperationSnapshot
    {
        public IReadOnlyList<OperationProgress> Active { get; set; } = new List<OperationProgress>();
    }
}