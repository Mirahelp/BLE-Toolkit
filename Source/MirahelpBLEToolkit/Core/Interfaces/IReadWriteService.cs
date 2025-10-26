using MirahelpBLEToolkit.Core.Enums;
using MirahelpBLEToolkit.Core.Results;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MirahelpBLEToolkit.Core.Interfaces
{
    public interface IReadWriteService
    {
        Task<GattReadResult> ReadAsync(UInt64 address, Guid serviceUuid, Guid characteristicUuid, CacheModeOptions cacheMode, CancellationToken cancellationToken);
        Task<GattWriteResult> WriteAsync(UInt64 address, Guid serviceUuid, Guid characteristicUuid, WriteTypeOptions writeType, Byte[] payload, CancellationToken cancellationToken);
        Task<(GattWriteResult Write, GattReadResult? Read, Byte[]? Notify)> WriteAndWaitNotifyAsync(UInt64 address, Guid serviceUuid, Guid writeCharacteristicUuid, Guid responseCharacteristicUuid, WriteTypeOptions writeType, Byte[] payload, TimeSpan timeout, CancellationToken cancellationToken);
        Task<(GattWriteResult Write, GattReadResult? Read, Byte[]? Notify)> WriteAndWaitNotifyAcrossServicesAsync(UInt64 address, Guid writeServiceUuid, Guid writeCharacteristicUuid, Guid responseServiceUuid, Guid responseCharacteristicUuid, WriteTypeOptions writeType, Byte[] payload, TimeSpan timeout, CancellationToken cancellationToken);
    }
}