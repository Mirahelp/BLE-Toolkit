using MirahelpBLEToolkit.Core.Enums;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MirahelpBLEToolkit.Core.Interfaces
{
    public interface IBleProviderService
    {
        Task<IReadOnlyList<IBleDeviceService>> GetPairedDevicesAsync(CancellationToken cancellationToken);
        IAdvertisementWatcherService CreateAdvertisementWatcher(ScanModeOptions mode);
        Task<IBleDeviceService?> FromAddressAsync(UInt64 address, CancellationToken cancellationToken);
        Task<IBleDeviceService?> FromIdAsync(String deviceId, CancellationToken cancellationToken);
    }
}