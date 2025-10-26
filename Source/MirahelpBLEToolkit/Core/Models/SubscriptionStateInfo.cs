using System;
using System.Collections.Generic;

namespace MirahelpBLEToolkit.Core.Models
{
    public sealed class SubscriptionStateInfo
    {
        public Int32 Count { get; set; }
        public Int32 Events { get; set; }
        public DateTime LastEventUtc { get; set; }
        public IReadOnlyList<SubscriptionEntry> Entries { get; set; } = new List<SubscriptionEntry>();
    }
}