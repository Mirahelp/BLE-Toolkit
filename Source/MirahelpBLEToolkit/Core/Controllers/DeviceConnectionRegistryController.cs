using MirahelpBLEToolkit.Core.Interfaces;
using MirahelpBLEToolkit.Core.Models;
using MirahelpBLEToolkit.Core.Services;
using System;
using System.Collections.Concurrent;

namespace MirahelpBLEToolkit.Core.Controllers
{
    public sealed class DeviceConnectionRegistryController : IConnectionOrchestratorRegistryService
    {
        private readonly ConnectionService _connectionService;
        private readonly IDeviceRepositoryService _deviceRepository;
        private readonly IEventBusService _eventBus;
        private readonly IClockService _clock;

        private readonly ConcurrentDictionary<UInt64, IDeviceConnectionControllerService> _controllerByAddress = new();

        public DeviceConnectionRegistryController(ConnectionService connectionService, IDeviceRepositoryService deviceRepository, IEventBusService eventBus, IClockService clock)
        {
            _connectionService = connectionService;
            _deviceRepository = deviceRepository;
            _eventBus = eventBus;
            _clock = clock;
        }

        public IDeviceConnectionControllerService Get(UInt64 address)
        {
            IDeviceConnectionControllerService controller = _controllerByAddress.GetOrAdd(address, (UInt64 a) => new DeviceConnectionController(a, _connectionService, _deviceRepository, _eventBus, _clock));
            return controller;
        }

        public DeviceConnectionSnapshot GetSnapshot(UInt64 address)
        {
            IDeviceConnectionControllerService controller = Get(address);
            DeviceConnectionSnapshot snapshot = controller.GetSnapshot();
            return snapshot;
        }

        public Guid RequestConnect(UInt64 address)
        {
            IDeviceConnectionControllerService controller = Get(address);
            Guid attemptIdentifier = controller.RequestConnect();
            return attemptIdentifier;
        }

        public void RequestDisconnect(UInt64 address, Boolean isManual)
        {
            IDeviceConnectionControllerService controller = Get(address);
            controller.RequestDisconnect(isManual);
        }

        public void SetAutoReconnectEnabled(UInt64 address, Boolean isEnabled)
        {
            IDeviceConnectionControllerService controller = Get(address);
            controller.SetAutoReconnectEnabled(isEnabled);
        }
    }
}