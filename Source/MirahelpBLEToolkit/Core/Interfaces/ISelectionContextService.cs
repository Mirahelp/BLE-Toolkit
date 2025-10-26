using System;

namespace MirahelpBLEToolkit.Core.Interfaces
{
    public interface ISelectionContextService
    {
        UInt64 SelectedAddress { get; set; }
        Int32 SelectedIndex { get; set; }
    }
}