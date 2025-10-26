using MirahelpBLEToolkit.Core.Models;
using System;

namespace MirahelpBLEToolkit.Core.Interfaces
{
    public interface ISubscriptionRepositoryService
    {
        void SetActive(UInt64 address, SubscriptionStateInfo state);
        SubscriptionStateInfo? TryGet(UInt64 address);
    }
}