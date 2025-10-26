using MirahelpBLEToolkit.Core.Enums;
using MirahelpBLEToolkit.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using GattCharacteristicsResult = MirahelpBLEToolkit.Core.Results.GattCharacteristicsResult;

namespace MirahelpBLEToolkit.Core.Platform
{
    public sealed class WindowsGattService : IGattServiceService
    {
        private readonly WindowsBleDevice _device;
        private readonly GattDeviceService _service;

        public Guid Uuid => _service.Uuid;

        public IBleDeviceService Device => _device;

        public WindowsGattService(WindowsBleDevice device, GattDeviceService service)
        {
            _device = device;
            _service = service;
        }

        public void Dispose()
        {
            try { _service.Dispose(); } catch { }
        }

        public async Task<GattCharacteristicsResult> GetCharacteristicsAsync(CacheModeOptions cacheMode, CancellationToken cancellationToken)
        {
            BluetoothCacheMode cm = cacheMode == CacheModeOptions.Cached ? BluetoothCacheMode.Cached : BluetoothCacheMode.Uncached;
            Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristicsResult windowsResult = await _service.GetCharacteristicsAsync(cm);
            List<IGattCharacteristicService> list = new();
            if (windowsResult != null && windowsResult.Characteristics != null)
            {
                foreach (GattCharacteristic c in windowsResult.Characteristics)
                {
                    list.Add(new WindowsGattCharacteristic(this, c));
                }
            }
            GattCharacteristicsResult result = new()
            {
                Characteristics = list,
                Status = WindowsGattCommon.MapStatus(windowsResult != null ? windowsResult.Status : Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.Unreachable)
            };
            return result;
        }

        public async Task<GattCharacteristicsResult> GetCharacteristicsForUuidAsync(Guid characteristicUuid, CancellationToken cancellationToken)
        {
            Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristicsResult windowsResult = await _service.GetCharacteristicsForUuidAsync(characteristicUuid);
            List<IGattCharacteristicService> list = new();
            if (windowsResult != null && windowsResult.Characteristics != null)
            {
                foreach (GattCharacteristic c in windowsResult.Characteristics)
                {
                    list.Add(new WindowsGattCharacteristic(this, c));
                }
            }
            GattCharacteristicsResult result = new()
            {
                Characteristics = list,
                Status = WindowsGattCommon.MapStatus(windowsResult != null ? windowsResult.Status : Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.Unreachable)
            };
            return result;
        }
    }
}