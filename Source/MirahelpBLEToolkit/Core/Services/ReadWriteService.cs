using MirahelpBLEToolkit.Core.Enums;
using MirahelpBLEToolkit.Core.Interfaces;
using MirahelpBLEToolkit.Core.Models;
using MirahelpBLEToolkit.Core.Results;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MirahelpBLEToolkit.Core.Services
{
    public sealed class ReadWriteService : IReadWriteService
    {
        private readonly IBleProviderService _provider;
        private readonly IMessageRepository _messageRepository;

        public ReadWriteService(IBleProviderService provider, IMessageRepository messageRepository)
        {
            _provider = provider;
            _messageRepository = messageRepository;
        }

        public async Task<GattReadResult> ReadAsync(UInt64 address, Guid serviceUuid, Guid characteristicUuid, CacheModeOptions cacheMode, CancellationToken cancellationToken)
        {
            Append(address, MessageDirectionOptions.Out, MessageKindOptions.ReadOut, serviceUuid, characteristicUuid, Array.Empty<Byte>(), String.Empty);

            IBleDeviceService? device = await _provider.FromAddressAsync(address, cancellationToken);
            if (device == null)
            {
                GattReadResult unreachable = new() { Status = GattCommunicationStatusOptions.Unreachable, Data = Array.Empty<Byte>() };
                return unreachable;
            }

            GattServicesResult servicesForUuid = await device.GetServicesForUuidAsync(serviceUuid, cancellationToken);
            if (servicesForUuid.Services.Count == 0)
            {
                device.Dispose();
                GattReadResult noService = new() { Status = servicesForUuid.Status, Data = Array.Empty<Byte>() };
                return noService;
            }

            IGattServiceService selectedService = servicesForUuid.Services[0];
            GattCharacteristicsResult characteristicsForUuid = await selectedService.GetCharacteristicsForUuidAsync(characteristicUuid, cancellationToken);
            if (characteristicsForUuid.Characteristics.Count == 0)
            {
                selectedService.Dispose();
                device.Dispose();
                GattReadResult noCharacteristic = new() { Status = characteristicsForUuid.Status, Data = Array.Empty<Byte>() };
                return noCharacteristic;
            }

            IGattCharacteristicService selectedCharacteristic = characteristicsForUuid.Characteristics[0];
            GattReadResult readResult = await selectedCharacteristic.ReadAsync(cacheMode, cancellationToken);
            if (readResult.Status == GattCommunicationStatusOptions.Success && readResult.Data != null)
            {
                Append(address, MessageDirectionOptions.In, MessageKindOptions.ReadIn, serviceUuid, characteristicUuid, readResult.Data, String.Empty);
            }

            selectedCharacteristic.Dispose();
            selectedService.Dispose();
            device.Dispose();
            return readResult;
        }

        public async Task<GattWriteResult> WriteAsync(UInt64 address, Guid serviceUuid, Guid characteristicUuid, WriteTypeOptions writeType, Byte[] payload, CancellationToken cancellationToken)
        {
            Append(address, MessageDirectionOptions.Out, MessageKindOptions.WriteOut, serviceUuid, characteristicUuid, payload ?? Array.Empty<Byte>(), String.Empty);

            IBleDeviceService? device = await _provider.FromAddressAsync(address, cancellationToken);
            if (device == null)
            {
                GattWriteResult unreachable = new() { Status = GattCommunicationStatusOptions.Unreachable };
                return unreachable;
            }

            GattServicesResult servicesForUuid = await device.GetServicesForUuidAsync(serviceUuid, cancellationToken);
            if (servicesForUuid.Services.Count == 0)
            {
                device.Dispose();
                GattWriteResult noService = new() { Status = servicesForUuid.Status };
                return noService;
            }

            IGattServiceService selectedService = servicesForUuid.Services[0];
            GattCharacteristicsResult characteristicsForUuid = await selectedService.GetCharacteristicsForUuidAsync(characteristicUuid, cancellationToken);
            if (characteristicsForUuid.Characteristics.Count == 0)
            {
                selectedService.Dispose();
                device.Dispose();
                GattWriteResult noCharacteristic = new() { Status = characteristicsForUuid.Status };
                return noCharacteristic;
            }

            IGattCharacteristicService selectedCharacteristic = characteristicsForUuid.Characteristics[0];
            GattWriteResult writeResult = await selectedCharacteristic.WriteAsync(payload ?? Array.Empty<Byte>(), writeType, cancellationToken);

            selectedCharacteristic.Dispose();
            selectedService.Dispose();
            device.Dispose();
            return writeResult;
        }

        public async Task<(GattWriteResult Write, GattReadResult? Read, Byte[]? Notify)> WriteAndWaitNotifyAsync(UInt64 address, Guid serviceUuid, Guid writeCharacteristicUuid, Guid responseCharacteristicUuid, WriteTypeOptions writeType, Byte[] payload, TimeSpan timeout, CancellationToken cancellationToken)
        {
            Append(address, MessageDirectionOptions.Out, MessageKindOptions.WriteOut, serviceUuid, writeCharacteristicUuid, payload ?? Array.Empty<Byte>(), String.Empty);

            IBleDeviceService? device = await _provider.FromAddressAsync(address, cancellationToken);
            if (device == null)
            {
                GattWriteResult unreachable = new() { Status = GattCommunicationStatusOptions.Unreachable };
                return (unreachable, null, null);
            }

            GattServicesResult servicesForUuid = await device.GetServicesForUuidAsync(serviceUuid, cancellationToken);
            if (servicesForUuid.Services.Count == 0)
            {
                device.Dispose();
                GattWriteResult noService = new() { Status = servicesForUuid.Status };
                return (noService, null, null);
            }

            IGattServiceService selectedService = servicesForUuid.Services[0];
            GattCharacteristicsResult allCharacteristics = await selectedService.GetCharacteristicsAsync(CacheModeOptions.Uncached, cancellationToken);

            IGattCharacteristicService? writeCharacteristic = null;
            IGattCharacteristicService? responseCharacteristic = null;

            foreach (IGattCharacteristicService characteristic in allCharacteristics.Characteristics)
            {
                if (characteristic.Uuid == writeCharacteristicUuid) writeCharacteristic = characteristic;
                if (characteristic.Uuid == responseCharacteristicUuid) responseCharacteristic = characteristic;
            }

            if (writeCharacteristic == null || responseCharacteristic == null)
            {
                if (writeCharacteristic != null) writeCharacteristic.Dispose();
                if (responseCharacteristic != null) responseCharacteristic.Dispose();
                selectedService.Dispose();
                device.Dispose();
                GattWriteResult notFound = new() { Status = GattCommunicationStatusOptions.Unreachable };
                return (notFound, null, null);
            }

            TaskCompletionSource<Byte[]> completionSource = new();
            Action<CharacteristicNotification> valueChangedHandler = delegate (CharacteristicNotification notification)
            {
                if (notification.Data != null && notification.Data.Length > 0)
                {
                    if (!completionSource.Task.IsCompleted) completionSource.TrySetResult(notification.Data);
                    Append(address, MessageDirectionOptions.In, MessageKindOptions.NotifyIn, serviceUuid, responseCharacteristic.Uuid, notification.Data, String.Empty);
                }
            };

            responseCharacteristic.ValueChanged += valueChangedHandler;
            await responseCharacteristic.ConfigureCccdAsync(CccdModeOptions.Notify, cancellationToken);

            GattWriteResult writeResult = await writeCharacteristic.WriteAsync(payload ?? Array.Empty<Byte>(), writeType, cancellationToken);

            Byte[]? notifyBytes = null;
            using (CancellationTokenSource linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                if (timeout > TimeSpan.Zero) linkedCancellation.CancelAfter(timeout);
                try
                {
                    using (linkedCancellation.Token.Register(delegate { try { completionSource.TrySetCanceled(); } catch { } }))
                    {
                    }
                    notifyBytes = await completionSource.Task;
                }
                catch
                {
                    notifyBytes = null;
                }
            }

            try { await responseCharacteristic.ConfigureCccdAsync(CccdModeOptions.None, cancellationToken); } catch { }
            try { responseCharacteristic.ValueChanged -= valueChangedHandler; } catch { }

            writeCharacteristic.Dispose();
            responseCharacteristic.Dispose();
            selectedService.Dispose();
            device.Dispose();

            return (writeResult, null, notifyBytes);
        }

        public async Task<(GattWriteResult Write, GattReadResult? Read, Byte[]? Notify)> WriteAndWaitNotifyAcrossServicesAsync(UInt64 address, Guid writeServiceUuid, Guid writeCharacteristicUuid, Guid responseServiceUuid, Guid responseCharacteristicUuid, WriteTypeOptions writeType, Byte[] payload, TimeSpan timeout, CancellationToken cancellationToken)
        {
            Append(address, MessageDirectionOptions.Out, MessageKindOptions.WriteOut, writeServiceUuid, writeCharacteristicUuid, payload ?? Array.Empty<Byte>(), String.Empty);

            IBleDeviceService? device = await _provider.FromAddressAsync(address, cancellationToken);
            if (device == null)
            {
                GattWriteResult unreachable = new() { Status = GattCommunicationStatusOptions.Unreachable };
                return (unreachable, null, null);
            }

            GattServicesResult writeServices = await device.GetServicesForUuidAsync(writeServiceUuid, cancellationToken);
            if (writeServices.Services.Count == 0)
            {
                device.Dispose();
                GattWriteResult noService = new() { Status = writeServices.Status };
                return (noService, null, null);
            }

            IGattServiceService writeService = writeServices.Services[0];
            GattCharacteristicsResult writeCharacteristics = await writeService.GetCharacteristicsForUuidAsync(writeCharacteristicUuid, cancellationToken);
            if (writeCharacteristics.Characteristics.Count == 0)
            {
                writeService.Dispose();
                device.Dispose();
                GattWriteResult noCharacteristic = new() { Status = writeCharacteristics.Status };
                return (noCharacteristic, null, null);
            }

            IGattCharacteristicService writeCharacteristic = writeCharacteristics.Characteristics[0];

            IGattServiceService? responseService = null;
            IGattCharacteristicService? responseCharacteristic = null;

            if (responseServiceUuid == writeServiceUuid)
            {
                GattCharacteristicsResult allCharacteristics = await writeService.GetCharacteristicsAsync(CacheModeOptions.Uncached, cancellationToken);
                foreach (IGattCharacteristicService characteristic in allCharacteristics.Characteristics)
                {
                    if (characteristic.Uuid == responseCharacteristicUuid)
                    {
                        responseCharacteristic = characteristic;
                        break;
                    }
                }
            }
            else
            {
                GattServicesResult responseServices = await device.GetServicesForUuidAsync(responseServiceUuid, cancellationToken);
                if (responseServices.Services.Count > 0)
                {
                    responseService = responseServices.Services[0];
                    GattCharacteristicsResult responseCharacteristics = await responseService.GetCharacteristicsForUuidAsync(responseCharacteristicUuid, cancellationToken);
                    if (responseCharacteristics.Characteristics.Count > 0)
                    {
                        responseCharacteristic = responseCharacteristics.Characteristics[0];
                    }
                }
            }

            if (responseCharacteristic == null)
            {
                writeCharacteristic.Dispose();
                writeService.Dispose();
                if (responseService != null) responseService.Dispose();
                device.Dispose();
                GattWriteResult notFound = new() { Status = GattCommunicationStatusOptions.Unreachable };
                return (notFound, null, null);
            }

            TaskCompletionSource<Byte[]> completionSource = new();
            Action<CharacteristicNotification> handler = delegate (CharacteristicNotification notification)
            {
                if (notification.Data != null && notification.Data.Length > 0)
                {
                    if (!completionSource.Task.IsCompleted) completionSource.TrySetResult(notification.Data);
                    Append(address, MessageDirectionOptions.In, MessageKindOptions.NotifyIn, responseServiceUuid, responseCharacteristicUuid, notification.Data, String.Empty);
                }
            };

            responseCharacteristic.ValueChanged += handler;
            await responseCharacteristic.ConfigureCccdAsync(CccdModeOptions.Notify, cancellationToken);

            GattWriteResult writeResult = await writeCharacteristic.WriteAsync(payload ?? Array.Empty<Byte>(), writeType, cancellationToken);

            Byte[]? notifyBytes = null;
            using (CancellationTokenSource linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                if (timeout > TimeSpan.Zero) linkedCancellation.CancelAfter(timeout);
                try
                {
                    using (linkedCancellation.Token.Register(delegate { try { completionSource.TrySetCanceled(); } catch { } }))
                    {
                    }
                    notifyBytes = await completionSource.Task;
                }
                catch
                {
                    notifyBytes = null;
                }
            }

            try { await responseCharacteristic.ConfigureCccdAsync(CccdModeOptions.None, cancellationToken); } catch { }
            try { responseCharacteristic.ValueChanged -= handler; } catch { }

            writeCharacteristic.Dispose();
            writeService.Dispose();
            if (responseCharacteristic != null) responseCharacteristic.Dispose();
            if (responseService != null) responseService.Dispose();
            device.Dispose();

            return (writeResult, null, notifyBytes);
        }

        private void Append(UInt64 address, MessageDirectionOptions direction, MessageKindOptions kind, Guid? service, Guid? characteristic, Byte[] data, String text)
        {
            MessageRecord record = new()
            {
                Address = address,
                TimestampUtc = DateTime.UtcNow,
                Direction = direction,
                Kind = kind,
                Service = service,
                Characteristic = characteristic,
                Data = data ?? Array.Empty<Byte>(),
                Text = text ?? String.Empty
            };
            _messageRepository.Add(address, record);
        }
    }
}