using System;

namespace MirahelpBLEToolkit.Core.Interfaces
{
    public interface IClockService
    {
        DateTime UtcNow { get; }
    }
}