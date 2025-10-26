using MirahelpBLEToolkit.Constants;
using MirahelpBLEToolkit.Core.Controllers;
using MirahelpBLEToolkit.Core.Enums;
using MirahelpBLEToolkit.Core.Events;
using MirahelpBLEToolkit.Core.Interfaces;
using MirahelpBLEToolkit.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MirahelpBLEToolkit.Core.Services
{
    public sealed class DiscoveryService : IDiscoveryService
    {
        private readonly IBleProviderService _provider;
        private readonly IDeviceRepositoryService _deviceRepository;
        private readonly IEventBusService _eventBus;
        private readonly IMessageRepository _messageRepository;
        private readonly IClockService _clock;
        private readonly NameResolutionService _nameResolutionService;

        private IAdvertisementWatcherService? _watcher;

        public DiscoveryService(IBleProviderService provider, IDeviceRepositoryService deviceRepository, IEventBusService eventBus, IMessageRepository messageRepository, IClockService clock, NameResolutionService nameResolutionService)
        {
            _provider = provider;
            _deviceRepository = deviceRepository;
            _eventBus = eventBus;
            _messageRepository = messageRepository;
            _clock = clock;
            _nameResolutionService = nameResolutionService;
            _watcher = null;
        }

        public void Start(ScanModeOptions mode)
        {
            _watcher = _provider.CreateAdvertisementWatcher(mode);
            _watcher.Received += OnReceived;
            _watcher.Start();
        }

        public void Stop()
        {
            if (_watcher != null)
            {
                _watcher.Received -= OnReceived;
                _watcher.Stop();
                _watcher = null;
            }
        }

        public async Task RefreshPairedAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<IBleDeviceService> pairedDevices = await _provider.GetPairedDevicesAsync(cancellationToken);
            foreach (IBleDeviceService device in pairedDevices)
            {
                String candidateName = device.Name ?? String.Empty;
                if (NameSelectionController.IsAddressLike(candidateName, device.BluetoothAddress)) candidateName = String.Empty;

                DeviceState deviceState = new()
                {
                    DeviceId = device.DeviceId,
                    Address = device.BluetoothAddress,
                    Name = candidateName,
                    IsPaired = true,
                    FirstSeenUtc = _clock.UtcNow,
                    LastSeenUtc = _clock.UtcNow,
                    Pinned = true,
                    ConnectionStatus = device.ConnectionStatus
                };
                _deviceRepository.Upsert(deviceState);
                if (String.IsNullOrWhiteSpace(deviceState.Name)) _nameResolutionService.Enqueue(deviceState.Address);
                DeviceUpsertedEventArgs upserted = new() { Device = deviceState, IsNew = true };
                _eventBus.Publish(upserted);
            }
        }

        private void OnReceived(AdvertisementReceivedEventArgs advertisementEventArgs)
        {
            _eventBus.Publish(advertisementEventArgs);

            String advertisementName = advertisementEventArgs.Name ?? String.Empty;
            if (NameSelectionController.IsAddressLike(advertisementName, advertisementEventArgs.Address)) advertisementName = String.Empty;

            DeviceState? existingState = _deviceRepository.TryGetByAddress(advertisementEventArgs.Address);
            if (existingState == null)
            {
                DeviceState newState = new()
                {
                    DeviceId = advertisementEventArgs.Address.ToString(AppStrings.FormatHexUpper),
                    Address = advertisementEventArgs.Address,
                    Name = advertisementName,
                    IsPaired = false,
                    FirstSeenUtc = _clock.UtcNow,
                    LastSeenUtc = _clock.UtcNow,
                    LastRssi = advertisementEventArgs.Rssi,
                    Pinned = false,
                    ConnectionStatus = ConnectionStatusOptions.Disconnected,
                    Manufacturer = advertisementEventArgs.Manufacturer ?? String.Empty,
                    AdvertisementType = advertisementEventArgs.AdvertisementType,
                    AdvertisedServiceUuids = advertisementEventArgs.ServiceUuids != null ? new List<Guid>(advertisementEventArgs.ServiceUuids) : new List<Guid>()
                };
                _deviceRepository.Upsert(newState);
                if (String.IsNullOrWhiteSpace(newState.Name)) _nameResolutionService.Enqueue(newState.Address);
                DeviceUpsertedEventArgs createdEvent = new() { Device = newState, IsNew = true };
                _eventBus.Publish(createdEvent);
            }
            else
            {
                existingState.LastSeenUtc = _clock.UtcNow;
                existingState.LastRssi = advertisementEventArgs.Rssi;
                if (!String.IsNullOrWhiteSpace(advertisementName)) existingState.Name = advertisementName;
                if (!String.IsNullOrWhiteSpace(advertisementEventArgs.Manufacturer)) existingState.Manufacturer = advertisementEventArgs.Manufacturer;
                if (advertisementEventArgs.ServiceUuids != null && advertisementEventArgs.ServiceUuids.Count > 0)
                {
                    HashSet<Guid> uuidSet = new(existingState.AdvertisedServiceUuids);
                    foreach (Guid serviceUuid in advertisementEventArgs.ServiceUuids) uuidSet.Add(serviceUuid);
                    existingState.AdvertisedServiceUuids = uuidSet.ToList();
                }
                _deviceRepository.Upsert(existingState);
                if (String.IsNullOrWhiteSpace(existingState.Name)) _nameResolutionService.Enqueue(existingState.Address);
                DeviceUpsertedEventArgs updatedEvent = new() { Device = existingState, IsNew = false };
                _eventBus.Publish(updatedEvent);
            }
        }
    }
}