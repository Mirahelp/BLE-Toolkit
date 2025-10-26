using MirahelpBLEToolkit.Core.Enums;
using MirahelpBLEToolkit.Core.Models;
using MirahelpBLEToolkit.Core.Results;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MirahelpBLEToolkit.Core.Interfaces
{
    public interface IGattCharacteristicService : IDisposable
    {
        Guid Uuid { get; }
        CharacteristicPropertyOptions Properties { get; }
        event Action<CharacteristicNotification>? ValueChanged;
        Task<GattReadResult> ReadAsync(CacheModeOptions cacheMode, CancellationToken cancellationToken);
        Task<GattWriteResult> WriteAsync(Byte[] payload, WriteTypeOptions writeType, CancellationToken cancellationToken);
        Task<GattCccdResult> ConfigureCccdAsync(CccdModeOptions mode, CancellationToken cancellationToken);
        IGattServiceService Service { get; }
    }
}