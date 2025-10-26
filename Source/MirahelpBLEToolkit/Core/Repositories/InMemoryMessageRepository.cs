using MirahelpBLEToolkit.Core.Enums;
using MirahelpBLEToolkit.Core.Interfaces;
using MirahelpBLEToolkit.Core.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace MirahelpBLEToolkit.Core.Repositories
{
    public sealed class InMemoryMessageRepository : IMessageRepository
    {
        private readonly ConcurrentDictionary<UInt64, List<MessageRecord>> _messagesByAddress = new();

        private Int32 _maxPerDevice = 1000;
        private Int64 _nextIdentifier = 0;

        public void Add(UInt64 address, MessageRecord messageRecord)
        {
            List<MessageRecord> list = _messagesByAddress.GetOrAdd(address, (UInt64 a) => new List<MessageRecord>());
            lock (list)
            {
                messageRecord.Id = Interlocked.Increment(ref _nextIdentifier);
                list.Add(messageRecord);
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

        public IReadOnlyList<MessageRecord> GetLatest(UInt64 address, MessageQuery query)
        {
            List<MessageRecord> list;
            Boolean found = _messagesByAddress.TryGetValue(address, out list);
            if (!found || list == null)
            {
                return new List<MessageRecord>();
            }
            List<MessageRecord> snapshot;
            lock (list)
            {
                snapshot = list.ToList();
            }
            IEnumerable<MessageRecord> filtered = snapshot.OrderByDescending(x => x.TimestampUtc);
            if (query.Direction.HasValue)
            {
                filtered = filtered.Where(x => x.Direction == query.Direction.Value);
            }
            if (query.Kinds != null && query.Kinds.Length > 0)
            {
                HashSet<MessageKindOptions> kindsSet = new(query.Kinds);
                filtered = filtered.Where(x => kindsSet.Contains(x.Kind));
            }
            if (query.Service.HasValue)
            {
                filtered = filtered.Where(x => x.Service.HasValue && x.Service.Value == query.Service.Value);
            }
            if (query.Characteristic.HasValue)
            {
                filtered = filtered.Where(x => x.Characteristic.HasValue && x.Characteristic.Value == query.Characteristic.Value);
            }
            if (query.SinceUtc.HasValue)
            {
                filtered = filtered.Where(x => x.TimestampUtc >= query.SinceUtc.Value);
            }
            List<MessageRecord> result = filtered.Take(Math.Max(1, query.Limit)).ToList();
            return result;
        }

        public MessageRecord? TryGetById(UInt64 address, Int64 id)
        {
            List<MessageRecord> list;
            Boolean found = _messagesByAddress.TryGetValue(address, out list);
            if (!found || list == null)
            {
                return null;
            }
            lock (list)
            {
                MessageRecord? record = list.FirstOrDefault(x => x.Id == id);
                return record;
            }
        }

        public void Clear(UInt64 address)
        {
            List<MessageRecord> removed;
            _messagesByAddress.TryRemove(address, out removed);
        }

        public void SetRetention(Int32 maxPerDevice)
        {
            _maxPerDevice = Math.Max(1, maxPerDevice);
        }
    }
}
