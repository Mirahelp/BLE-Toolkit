using MirahelpBLEToolkit.Core.Enums;
using MirahelpBLEToolkit.Core.Results;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MirahelpBLEToolkit.Core.Interfaces
{
    public interface IGattServiceService : IDisposable
    {
        Guid Uuid { get; }
        Task<GattCharacteristicsResult> GetCharacteristicsAsync(CacheModeOptions cacheMode, CancellationToken cancellationToken);
        Task<GattCharacteristicsResult> GetCharacteristicsForUuidAsync(Guid characteristicUuid, CancellationToken cancellationToken);
        IBleDeviceService Device { get; }
    }
}