using MirahelpBLEToolkit.Core.Interfaces;
using MirahelpBLEToolkit.Core.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace MirahelpBLEToolkit.Core.Repositories
{
    public sealed class InMemoryDeviceRepository : IDeviceRepositoryService
    {
        private readonly ConcurrentDictionary<UInt64, DeviceState> _deviceStatesByAddress = new();
        private Int32 _nextNumber = 0;
        private Int32 _nextOrdinal = 0;

        public void Upsert(DeviceState deviceState)
        {
            DeviceState existing;
            Boolean exists = _deviceStatesByAddress.TryGetValue(deviceState.Address, out existing);
            if (!exists)
            {
                Int32 numberAssigned = Interlocked.Increment(ref _nextNumber);
                Int32 ordinalAssigned = Interlocked.Increment(ref _nextOrdinal);
                deviceState.Number = numberAssigned;
                deviceState.Ordinal = ordinalAssigned;
                if (deviceState.FirstSeenUtc == DateTime.MinValue)
                {
                    deviceState.FirstSeenUtc = DateTime.UtcNow;
                }
                _deviceStatesByAddress.TryAdd(deviceState.Address, deviceState);
                return;
            }
            deviceState.Number = existing.Number;
            deviceState.Ordinal = existing.Ordinal;
            if (existing.FirstSeenUtc != DateTime.MinValue)
            {
                deviceState.FirstSeenUtc = existing.FirstSeenUtc;
            }
            _deviceStatesByAddress[deviceState.Address] = deviceState;
        }

        public DeviceState? TryGetByAddress(UInt64 address)
        {
            DeviceState value;
            Boolean found = _deviceStatesByAddress.TryGetValue(address, out value);
            if (found)
            {
                return value;
            }
            return null;
        }

        public IReadOnlyList<DeviceState> GetAll()
        {
            List<DeviceState> list = _deviceStatesByAddress.Values.ToList();
            return list;
        }

        public void Remove(UInt64 address)
        {
            DeviceState removed;
            _deviceStatesByAddress.TryRemove(address, out removed);
        }

        public void SetPinned(UInt64 address, Boolean pinned)
        {
            DeviceState? state = TryGetByAddress(address);
            if (state != null)
            {
                state.Pinned = pinned;
                Upsert(state);
            }
        }
    }
}
