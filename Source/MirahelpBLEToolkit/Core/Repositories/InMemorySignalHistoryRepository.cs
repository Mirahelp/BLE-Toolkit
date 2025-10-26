using MirahelpBLEToolkit.Core.Interfaces;
using MirahelpBLEToolkit.Core.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace MirahelpBLEToolkit.Core.Repositories
{
    public sealed class InMemorySignalHistoryRepository : ISignalHistoryRepositoryService
    {
        private readonly ConcurrentDictionary<UInt64, List<RssiSample>> _samplesByAddress = new();
        private Int32 _maxPerDevice = 1024;

        public void Append(UInt64 address, RssiSample sample)
        {
            List<RssiSample> list = _samplesByAddress.GetOrAdd(address, (UInt64 a) => new List<RssiSample>());
            lock (list)
            {
                list.Add(sample);
                if (list.Count > _maxPerDevice)
                {
                    Int32 toRemove = list.Count - _maxPerDevice;
                    if (toRemove > 0 && toRemove < list.Count)
                    {
                        list.RemoveRange(0, toRemove);
                    }
                }
            }
        }

        public IReadOnlyList<RssiSample> GetLatest(UInt64 address, Int32 maxCount)
        {
            List<RssiSample> list;
            Boolean found = _samplesByAddress.TryGetValue(address, out list);
            if (!found || list == null)
            {
                return new List<RssiSample>();
            }
            List<RssiSample> snapshot;
            lock (list)
            {
                snapshot = list.ToList();
            }
            List<RssiSample> result = snapshot.TakeLast(Math.Max(1, maxCount)).ToList();
            return result;
        }

        public void Clear(UInt64 address)
        {
            List<RssiSample> removed;
            _samplesByAddress.TryRemove(address, out removed);
        }

        public void SetRetentionPerDevice(Int32 maxCount)
        {
            _maxPerDevice = Math.Max(1, maxCount);
        }
    }
}
