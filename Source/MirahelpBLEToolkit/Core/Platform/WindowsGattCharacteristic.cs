using MirahelpBLEToolkit.Core.Enums;
using MirahelpBLEToolkit.Core.Interfaces;
using MirahelpBLEToolkit.Core.Models;
using MirahelpBLEToolkit.Core.Results;
using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;
using GattReadResult = MirahelpBLEToolkit.Core.Results.GattReadResult;
using GattWriteResult = MirahelpBLEToolkit.Core.Results.GattWriteResult;

namespace MirahelpBLEToolkit.Core.Platform
{
    public sealed class WindowsGattCharacteristic : IGattCharacteristicService
    {
        private readonly WindowsGattService _service;
        private readonly GattCharacteristic _characteristic;

        public Guid Uuid => _characteristic.Uuid;

        public CharacteristicPropertyOptions Properties => WindowsGattCommon.MapProperties(_characteristic.CharacteristicProperties);

        public IGattServiceService Service => _service;

        public event Action<CharacteristicNotification>? ValueChanged;

        public WindowsGattCharacteristic(WindowsGattService service, GattCharacteristic characteristic)
        {
            _service = service;
            _characteristic = characteristic;
            _characteristic.ValueChanged += OnValueChanged;
        }

        public void Dispose()
        {
            try
            {
                _characteristic.ValueChanged -= OnValueChanged;
            }
            catch
            {
            }
        }

        public async Task<GattReadResult> ReadAsync(CacheModeOptions cacheMode, CancellationToken cancellationToken)
        {
            BluetoothCacheMode cm = cacheMode == CacheModeOptions.Cached ? BluetoothCacheMode.Cached : BluetoothCacheMode.Uncached;
            GattReadResult result = new();
            Windows.Devices.Bluetooth.GenericAttributeProfile.GattReadResult readResult = await _characteristic.ReadValueAsync(cm);
            result.Status = WindowsGattCommon.MapStatus(readResult != null ? readResult.Status : Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.Unreachable);
            if (readResult != null && readResult.Value != null)
            {
                Byte[] data = BufferToBytes(readResult.Value);
                result.Data = data;
            }
            else
            {
                result.Data = Array.Empty<Byte>();
            }
            return result;
        }

        public async Task<GattWriteResult> WriteAsync(Byte[] payload, WriteTypeOptions writeType, CancellationToken cancellationToken)
        {
            IBuffer buffer = CryptographicBuffer.CreateFromByteArray(payload ?? Array.Empty<Byte>());
            GattWriteOption option = writeType == WriteTypeOptions.WithoutResponse ? GattWriteOption.WriteWithoutResponse : GattWriteOption.WriteWithResponse;
            Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus status = await _characteristic.WriteValueAsync(buffer, option);
            GattWriteResult result = new()
            {
                Status = WindowsGattCommon.MapStatus(status)
            };
            return result;
        }

        public async Task<GattCccdResult> ConfigureCccdAsync(CccdModeOptions mode, CancellationToken cancellationToken)
        {
            GattClientCharacteristicConfigurationDescriptorValue value = GattClientCharacteristicConfigurationDescriptorValue.None;
            if (mode == CccdModeOptions.Notify)
            {
                value = GattClientCharacteristicConfigurationDescriptorValue.Notify;
            }
            else if (mode == CccdModeOptions.Indicate)
            {
                value = GattClientCharacteristicConfigurationDescriptorValue.Indicate;
            }
            Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus status = await _characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(value);
            GattCccdResult result = new()
            {
                Status = WindowsGattCommon.MapStatus(status)
            };
            return result;
        }

        private void OnValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            Action<CharacteristicNotification>? handler = ValueChanged;
            if (handler != null)
            {
                CharacteristicNotification notification = new()
                {
                    Address = sender != null && sender.Service != null && sender.Service.Device != null ? sender.Service.Device.BluetoothAddress : 0,
                    Service = sender != null && sender.Service != null ? sender.Service.Uuid : Guid.Empty,
                    Characteristic = sender != null ? sender.Uuid : Guid.Empty,
                    TimestampUtc = args.Timestamp.UtcDateTime,
                    Data = args != null && args.CharacteristicValue != null ? BufferToBytes(args.CharacteristicValue) : Array.Empty<Byte>()
                };
                handler(notification);
            }
        }

        private static Byte[] BufferToBytes(IBuffer buffer)
        {
            if (buffer == null || buffer.Length == 0)
            {
                return Array.Empty<Byte>();
            }
            Byte[] bytes = new Byte[buffer.Length];
            DataReader.FromBuffer(buffer).ReadBytes(bytes);
            return bytes;
        }
    }
}
