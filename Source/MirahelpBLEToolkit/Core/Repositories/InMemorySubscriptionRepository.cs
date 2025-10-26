using MirahelpBLEToolkit.Core.Interfaces;
using MirahelpBLEToolkit.Core.Models;
using System;
using System.Collections.Concurrent;

namespace MirahelpBLEToolkit.Core.Repositories
{
    public sealed class InMemorySubscriptionRepository : ISubscriptionRepositoryService
    {
        private readonly ConcurrentDictionary<UInt64, SubscriptionStateInfo> _subscriptionByAddress = new();

        public void SetActive(UInt64 address, SubscriptionStateInfo state)
        {
            _subscriptionByAddress[address] = state;
        }

        public SubscriptionStateInfo? TryGet(UInt64 address)
        {
            SubscriptionStateInfo value;
            Boolean found = _subscriptionByAddress.TryGetValue(address, out value);
            if (found)
            {
                return value;
            }
            return null;
        }
    }
}
