using MirahelpBLEToolkit.Core.Enums;
using MirahelpBLEToolkit.Core.Interfaces;
using MirahelpBLEToolkit.Core.Models;
using MirahelpBLEToolkit.Core.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MirahelpBLEToolkit.Core.Services
{
    public sealed class GattBrowserService : IGattBrowserService
    {
        private readonly IBleProviderService _provider;
        private readonly IMessageRepository _messageRepository;

        public GattBrowserService(IBleProviderService provider, IMessageRepository messageRepository)
        {
            _provider = provider;
            _messageRepository = messageRepository;
        }

        public async Task<GattServicesResult> ListServicesAsync(UInt64 address, CacheModeOptions cacheMode, CancellationToken cancellationToken)
        {
            Append(address, MessageDirectionOptions.Out, MessageKindOptions.ServiceQueryOut, null, null, Array.Empty<Byte>(), String.Empty);
            IBleDeviceService? device = await _provider.FromAddressAsync(address, cancellationToken);
            if (device == null)
            {
                GattServicesResult emptyList = new()
                {
                    Services = new List<IGattServiceService>(),
                    Status = GattCommunicationStatusOptions.Unreachable
                };
                return emptyList;
            }
            GattServicesResult services = await device.GetServicesAsync(cacheMode, cancellationToken);
            if (services.Services != null)
            {
                foreach (IGattServiceService service in services.Services)
                {
                    Append(address, MessageDirectionOptions.In, MessageKindOptions.ServiceQueryIn, service.Uuid, null, Array.Empty<Byte>(), String.Empty);
                }
            }
            return services;
        }

        public async Task<GattCharacteristicsResult> ListCharacteristicsAsync(UInt64 address, Guid serviceUuid, CacheModeOptions cacheMode, CancellationToken cancellationToken)
        {
            Append(address, MessageDirectionOptions.Out, MessageKindOptions.CharQueryOut, serviceUuid, null, Array.Empty<Byte>(), String.Empty);
            IBleDeviceService? device = await _provider.FromAddressAsync(address, cancellationToken);
            if (device == null)
            {
                GattCharacteristicsResult emptyList = new()
                {
                    Characteristics = new List<IGattCharacteristicService>(),
                    Status = GattCommunicationStatusOptions.Unreachable
                };
                return emptyList;
            }
            GattServicesResult services = await device.GetServicesForUuidAsync(serviceUuid, cancellationToken);
            if (services.Services.Count == 0)
            {
                device.Dispose();
                GattCharacteristicsResult none = new()
                {
                    Characteristics = new List<IGattCharacteristicService>(),
                    Status = services.Status
                };
                return none;
            }
            IGattServiceService chosenService = services.Services.First();
            GattCharacteristicsResult characteristicList = await chosenService.GetCharacteristicsAsync(cacheMode, cancellationToken);
            if (characteristicList.Characteristics != null)
            {
                foreach (IGattCharacteristicService characteristic in characteristicList.Characteristics)
                {
                    Append(address, MessageDirectionOptions.In, MessageKindOptions.CharQueryIn, serviceUuid, characteristic.Uuid, Array.Empty<Byte>(), String.Empty);
                }
            }
            chosenService.Dispose();
            device.Dispose();
            return characteristicList;
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