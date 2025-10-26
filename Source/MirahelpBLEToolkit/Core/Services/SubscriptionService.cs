using MirahelpBLEToolkit.Core.Enums;
using MirahelpBLEToolkit.Core.Interfaces;
using MirahelpBLEToolkit.Core.Models;
using MirahelpBLEToolkit.Core.Results;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MirahelpBLEToolkit.Core.Services
{
    public sealed class SubscriptionService : ISubscriptionService
    {
        private readonly IBleProviderService _provider;
        private readonly ISubscriptionRepositoryService _subscriptionRepository;
        private readonly IMessageRepository _messageRepository;
        private readonly IEventBusService _eventBus;

        private readonly Dictionary<IGattCharacteristicService, Action<CharacteristicNotification>> _notificationHandlersByCharacteristic = new();

        public SubscriptionService(IBleProviderService provider, ISubscriptionRepositoryService subscriptionRepository, IMessageRepository messageRepository, IEventBusService eventBus)
        {
            _provider = provider;
            _subscriptionRepository = subscriptionRepository;
            _messageRepository = messageRepository;
            _eventBus = eventBus;
        }

        public async Task<Int32> SubscribeAllAsync(UInt64 address, CancellationToken cancellationToken)
        {
            IBleDeviceService? device = await _provider.FromAddressAsync(address, cancellationToken);
            if (device == null)
            {
                return 0;
            }
            GattServicesResult services = await device.GetServicesAsync(CacheModeOptions.Cached, cancellationToken);
            Int32 subscribedCount = 0;
            foreach (IGattServiceService service in services.Services)
            {
                GattCharacteristicsResult characteristics = await service.GetCharacteristicsAsync(CacheModeOptions.Cached, cancellationToken);
                foreach (IGattCharacteristicService characteristic in characteristics.Characteristics)
                {
                    Boolean canNotify = (characteristic.Properties & CharacteristicPropertyOptions.Notify) == CharacteristicPropertyOptions.Notify || (characteristic.Properties & CharacteristicPropertyOptions.Indicate) == CharacteristicPropertyOptions.Indicate;
                    if (!canNotify)
                    {
                        continue;
                    }
                    Action<CharacteristicNotification> handler = delegate (CharacteristicNotification notification)
                    {
                        if (notification.Data != null && notification.Data.Length > 0)
                        {
                            MessageRecord record = new()
                            {
                                Address = address,
                                TimestampUtc = DateTime.UtcNow,
                                Direction = MessageDirectionOptions.In,
                                Kind = MessageKindOptions.NotifyIn,
                                Service = service.Uuid,
                                Characteristic = characteristic.Uuid,
                                Data = notification.Data,
                                Text = String.Empty
                            };
                            _messageRepository.Add(address, record);
                        }
                    };
                    try
                    {
                        characteristic.ValueChanged += handler;
                        _notificationHandlersByCharacteristic[characteristic] = handler;
                    }
                    catch
                    {
                    }
                    AppendCccdRecord(address, service.Uuid, characteristic.Uuid, CccdModeOptions.Notify);
                    await characteristic.ConfigureCccdAsync(CccdModeOptions.Notify, cancellationToken);
                    subscribedCount++;
                }
            }
            SubscriptionStateInfo state = new()
            {
                Count = subscribedCount,
                Events = 0,
                LastEventUtc = DateTime.MinValue,
                Entries = new List<SubscriptionEntry>()
            };
            _subscriptionRepository.SetActive(address, state);
            device.Dispose();
            return subscribedCount;
        }

        public async Task<Int32> UnsubscribeAllAsync(UInt64 address, CancellationToken cancellationToken)
        {
            IBleDeviceService? device = await _provider.FromAddressAsync(address, cancellationToken);
            if (device == null)
            {
                return 0;
            }
            GattServicesResult services = await device.GetServicesAsync(CacheModeOptions.Cached, cancellationToken);
            Int32 removedCount = 0;
            foreach (IGattServiceService service in services.Services)
            {
                GattCharacteristicsResult characteristics = await service.GetCharacteristicsAsync(CacheModeOptions.Cached, cancellationToken);
                foreach (IGattCharacteristicService characteristic in characteristics.Characteristics)
                {
                    try
                    {
                        Action<CharacteristicNotification>? handler;
                        Boolean hadHandler = _notificationHandlersByCharacteristic.TryGetValue(characteristic, out handler);
                        if (hadHandler && handler != null)
                        {
                            try { characteristic.ValueChanged -= handler; } catch { }
                            _notificationHandlersByCharacteristic.Remove(characteristic);
                        }
                    }
                    catch
                    {
                    }
                    AppendCccdRecord(address, service.Uuid, characteristic.Uuid, CccdModeOptions.None);
                    await characteristic.ConfigureCccdAsync(CccdModeOptions.None, cancellationToken);
                    removedCount++;
                }
            }
            SubscriptionStateInfo state = new()
            {
                Count = 0,
                Events = 0,
                LastEventUtc = DateTime.MinValue,
                Entries = new List<SubscriptionEntry>()
            };
            _subscriptionRepository.SetActive(address, state);
            device.Dispose();
            return removedCount;
        }

        public async Task<Boolean> ToggleAsync(UInt64 address, Guid serviceUuid, Guid characteristicUuid, CancellationToken cancellationToken)
        {
            IBleDeviceService? device = await _provider.FromAddressAsync(address, cancellationToken);
            if (device == null)
            {
                return false;
            }
            GattServicesResult services = await device.GetServicesForUuidAsync(serviceUuid, cancellationToken);
            if (services.Services.Count == 0)
            {
                device.Dispose();
                return false;
            }
            IGattServiceService service = services.Services[0];
            GattCharacteristicsResult list = await service.GetCharacteristicsForUuidAsync(characteristicUuid, cancellationToken);
            if (list.Characteristics.Count == 0)
            {
                service.Dispose();
                device.Dispose();
                return false;
            }
            IGattCharacteristicService characteristic = list.Characteristics[0];
            AppendCccdRecord(address, serviceUuid, characteristicUuid, CccdModeOptions.Notify);
            await characteristic.ConfigureCccdAsync(CccdModeOptions.Notify, cancellationToken);
            device.Dispose();
            return true;
        }

        private void AppendCccdRecord(UInt64 address, Guid serviceUuid, Guid characteristicUuid, CccdModeOptions mode)
        {
            MessageRecord record = new()
            {
                Address = address,
                TimestampUtc = DateTime.UtcNow,
                Direction = MessageDirectionOptions.Out,
                Kind = MessageKindOptions.CccdWriteOut,
                Service = serviceUuid,
                Characteristic = characteristicUuid,
                Data = Array.Empty<Byte>(),
                Text = mode.ToString()
            };
            _messageRepository.Add(address, record);
        }
    }
}