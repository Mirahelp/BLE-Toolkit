using MirahelpBLEToolkit.Core.Enums;
using MirahelpBLEToolkit.Core.Models;
using MirahelpBLEToolkit.Core.Results;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MirahelpBLEToolkit.Core.Interfaces
{
    public interface IBleDeviceService : IDisposable
    {
        UInt64 BluetoothAddress { get; }
        String DeviceId { get; }
        String Name { get; }
        Boolean IsPaired { get; }
        ConnectionStatusOptions ConnectionStatus { get; }
        event Action<ConnectionStatusOptions>? ConnectionStatusChanged;
        Task<GattSessionInfo> GetGattSessionInfoAsync(CancellationToken cancellationToken);
        Task<GattServicesResult> GetServicesAsync(CacheModeOptions cacheMode, CancellationToken cancellationToken);
        Task<GattServicesResult> GetServicesForUuidAsync(Guid serviceUuid, CancellationToken cancellationToken);
    }
}