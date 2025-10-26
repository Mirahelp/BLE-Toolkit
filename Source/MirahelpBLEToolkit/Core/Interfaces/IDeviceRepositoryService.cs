using MirahelpBLEToolkit.Core.Models;
using System;
using System.Collections.Generic;

namespace MirahelpBLEToolkit.Core.Interfaces
{
    public interface IDeviceRepositoryService
    {
        void Upsert(DeviceState deviceState);
        DeviceState? TryGetByAddress(UInt64 address);
        IReadOnlyList<DeviceState> GetAll();
        void Remove(UInt64 address);
        void SetPinned(UInt64 address, Boolean pinned);
    }
}