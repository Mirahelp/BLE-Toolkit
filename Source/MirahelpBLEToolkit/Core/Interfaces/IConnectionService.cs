using MirahelpBLEToolkit.Core.Enums;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MirahelpBLEToolkit.Core.Interfaces
{
    public interface IConnectionService
    {
        Task<Boolean> EnsureConnectedAsync(UInt64 address, CacheModeOptions cacheMode, CancellationToken cancellationToken);
        Task<Boolean> ProbeSessionAsync(UInt64 address, CancellationToken cancellationToken);
        void Disconnect(UInt64 address);
    }
}