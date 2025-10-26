using System;

namespace MirahelpBLEToolkit.Core.Models
{
    public sealed class SubscriptionEntry
    {
        public Guid Service { get; set; }
        public Guid Characteristic { get; set; }
        public MirahelpBLEToolkit.Core.Enums.CccdModeOptions Mode { get; set; }
    }
}