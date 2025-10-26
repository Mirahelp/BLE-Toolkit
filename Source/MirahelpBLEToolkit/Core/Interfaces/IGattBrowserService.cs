using MirahelpBLEToolkit.Core.Enums;
using MirahelpBLEToolkit.Core.Results;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MirahelpBLEToolkit.Core.Interfaces
{
    public interface IGattBrowserService
    {
        Task<GattServicesResult> ListServicesAsync(UInt64 address, CacheModeOptions cacheMode, CancellationToken cancellationToken);
        Task<GattCharacteristicsResult> ListCharacteristicsAsync(UInt64 address, Guid serviceUuid, CacheModeOptions cacheMode, CancellationToken cancellationToken);
    }
}