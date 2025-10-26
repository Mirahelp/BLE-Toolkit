using MirahelpBLEToolkit.Core.Enums;
using MirahelpBLEToolkit.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace MirahelpBLEToolkit.Core.Platform
{
    public sealed class WindowsBleProvider : IBleProviderService
    {
        public IAdvertisementWatcherService CreateAdvertisementWatcher(ScanModeOptions mode)
        {
            IAdvertisementWatcherService watcher = new WindowsAdvertisementWatcher(mode);
            return watcher;
        }

        public async Task<IBleDeviceService?> FromAddressAsync(UInt64 address, CancellationToken cancellationToken)
        {
            try
            {
                BluetoothLEDevice? device = await BluetoothLEDevice.FromBluetoothAddressAsync(address);
                if (device == null)
                {
                    return null;
                }
                IBleDeviceService adapter = new WindowsBleDevice(device);
                return adapter;
            }
            catch
            {
                return null;
            }
        }

        public async Task<IBleDeviceService?> FromIdAsync(String deviceId, CancellationToken cancellationToken)
        {
            try
            {
                BluetoothLEDevice? device = await BluetoothLEDevice.FromIdAsync(deviceId);
                if (device == null)
                {
                    return null;
                }
                IBleDeviceService adapter = new WindowsBleDevice(device);
                return adapter;
            }
            catch
            {
                return null;
            }
        }

        public async Task<IReadOnlyList<IBleDeviceService>> GetPairedDevicesAsync(CancellationToken cancellationToken)
        {
            List<IBleDeviceService> list = new();
            String selector = BluetoothLEDevice.GetDeviceSelectorFromPairingState(true);
            DeviceInformationCollection infos;
            try
            {
                infos = await DeviceInformation.FindAllAsync(selector);
            }
            catch
            {
                return list;
            }

            foreach (DeviceInformation info in infos)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                BluetoothLEDevice? device = null;
                try
                {
                    device = await BluetoothLEDevice.FromIdAsync(info.Id);
                    if (device != null)
                    {
                        IBleDeviceService adapter = new WindowsBleDevice(device);
                        list.Add(adapter);
                        device = null;
                    }
                }
                catch
                {
                    if (device != null)
                    {
                        try { device.Dispose(); } catch { }
                    }
                }
            }
            return list;
        }
    }
}