using MirahelpBLEToolkit.Core.Enums;
using MirahelpBLEToolkit.Core.Interfaces;
using MirahelpBLEToolkit.Core.Models;
using MirahelpBLEToolkit.Core.Results;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace MirahelpBLEToolkit.Core.Platform
{
    public sealed class WindowsBleDevice : IBleDeviceService
    {
        private readonly BluetoothLEDevice _device;

        public UInt64 BluetoothAddress => _device.BluetoothAddress;

        public String DeviceId => _device.DeviceId;

        public String Name
        {
            get
            {
                try
                {
                    String primary = _device.Name ?? String.Empty;
                    if (!String.IsNullOrWhiteSpace(primary)) return primary;
                    String display = _device.DeviceInformation != null ? (_device.DeviceInformation.Name ?? String.Empty) : String.Empty;
                    if (!String.IsNullOrWhiteSpace(display)) return display;
                }
                catch
                {
                }
                return String.Empty;
            }
        }

        public Boolean IsPaired
        {
            get
            {
                try
                {
                    return _device.DeviceInformation != null && _device.DeviceInformation.Pairing != null && _device.DeviceInformation.Pairing.IsPaired;
                }
                catch
                {
                    return false;
                }
            }
        }

        public ConnectionStatusOptions ConnectionStatus => _device.ConnectionStatus == BluetoothConnectionStatus.Connected ? ConnectionStatusOptions.Connected : ConnectionStatusOptions.Disconnected;

        public event Action<ConnectionStatusOptions>? ConnectionStatusChanged;

        public WindowsBleDevice(BluetoothLEDevice device)
        {
            _device = device;
            _device.ConnectionStatusChanged += OnConnectionStatusChanged;
        }

        public void Dispose()
        {
            try
            {
                _device.ConnectionStatusChanged -= OnConnectionStatusChanged;
            }
            catch
            {
            }
            try
            {
                _device.Dispose();
            }
            catch
            {
            }
        }

        public async Task<GattSessionInfo> GetGattSessionInfoAsync(CancellationToken cancellationToken)
        {
            GattSessionInfo info = new()
            {
                MaxPdu = 0,
                MaintainConnection = false,
                SessionStatus = String.Empty
            };
            GattDeviceServicesResult result = await _device.GetGattServicesAsync(BluetoothCacheMode.Cached);
            if (result != null && result.Status == Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.Success && result.Services != null && result.Services.Count > 0)
            {
                GattDeviceService first = result.Services[0];
                if (first.Session != null)
                {
                    info.MaxPdu = first.Session.MaxPduSize;
                    info.MaintainConnection = first.Session.MaintainConnection;
                    info.SessionStatus = first.Session.SessionStatus.ToString();
                }
                foreach (GattDeviceService s in result.Services)
                {
                    try { s.Dispose(); } catch { }
                }
            }
            return info;
        }

        public async Task<GattServicesResult> GetServicesAsync(CacheModeOptions cacheMode, CancellationToken cancellationToken)
        {
            BluetoothCacheMode cm = cacheMode == CacheModeOptions.Cached ? BluetoothCacheMode.Cached : BluetoothCacheMode.Uncached;
            GattDeviceServicesResult result = await _device.GetGattServicesAsync(cm);
            List<IGattServiceService> services = new();
            if (result != null && result.Services != null)
            {
                foreach (GattDeviceService s in result.Services)
                {
                    services.Add(new WindowsGattService(this, s));
                }
            }
            GattServicesResult list = new()
            {
                Services = services,
                Status = WindowsGattCommon.MapStatus(result != null ? result.Status : Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.Unreachable),
                Device = this
            };
            return list;
        }

        public async Task<GattServicesResult> GetServicesForUuidAsync(Guid serviceUuid, CancellationToken cancellationToken)
        {
            GattDeviceServicesResult result = await _device.GetGattServicesForUuidAsync(serviceUuid);
            List<IGattServiceService> services = new();
            if (result != null && result.Services != null)
            {
                foreach (GattDeviceService s in result.Services)
                {
                    services.Add(new WindowsGattService(this, s));
                }
            }
            GattServicesResult list = new()
            {
                Services = services,
                Status = WindowsGattCommon.MapStatus(result != null ? result.Status : Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.Unreachable),
                Device = this
            };
            return list;
        }

        private void OnConnectionStatusChanged(BluetoothLEDevice sender, Object args)
        {
            Action<ConnectionStatusOptions>? handler = ConnectionStatusChanged;
            if (handler != null)
            {
                handler(sender.ConnectionStatus == BluetoothConnectionStatus.Connected ? ConnectionStatusOptions.Connected : ConnectionStatusOptions.Disconnected);
            }
        }
    }
}