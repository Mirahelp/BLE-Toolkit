using MirahelpBLEToolkit.Core.Interfaces;
using System;

namespace MirahelpBLEToolkit.Core.Controllers
{
    public sealed class SystemClockService : IClockService
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }
}
