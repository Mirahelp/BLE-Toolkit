using System;
using System.Threading;
using System.Threading.Tasks;

namespace MirahelpBLEToolkit.Core.Interfaces
{
    public interface ISubscriptionService
    {
        Task<Int32> SubscribeAllAsync(UInt64 address, CancellationToken cancellationToken);
        Task<Int32> UnsubscribeAllAsync(UInt64 address, CancellationToken cancellationToken);
        Task<Boolean> ToggleAsync(UInt64 address, Guid serviceUuid, Guid characteristicUuid, CancellationToken cancellationToken);
    }
}