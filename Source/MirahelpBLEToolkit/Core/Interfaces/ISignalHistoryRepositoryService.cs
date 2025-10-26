using MirahelpBLEToolkit.Core.Models;
using System;
using System.Collections.Generic;

namespace MirahelpBLEToolkit.Core.Interfaces
{
    public interface ISignalHistoryRepositoryService
    {
        void Append(UInt64 address, RssiSample sample);
        IReadOnlyList<RssiSample> GetLatest(UInt64 address, Int32 maxCount);
        void Clear(UInt64 address);
        void SetRetentionPerDevice(Int32 maxCount);
    }
}