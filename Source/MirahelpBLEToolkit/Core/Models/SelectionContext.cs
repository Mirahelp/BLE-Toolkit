using MirahelpBLEToolkit.Core.Interfaces;
using System;

namespace MirahelpBLEToolkit.Core.Models
{
    public sealed class SelectionContext : ISelectionContextService
    {
        public UInt64 SelectedAddress { get; set; }
        public Int32 SelectedIndex { get; set; }
    }
}
