using MirahelpBLEToolkit.Core.Controllers;
using MirahelpBLEToolkit.Core.Enums;
using MirahelpBLEToolkit.Core.Interfaces;
using MirahelpBLEToolkit.Core.Platform;
using MirahelpBLEToolkit.Core.Repositories;
using MirahelpBLEToolkit.Core.Services;
using System;

public static class AppHost
{
    private static readonly Object SynchronizationObject = new();
    private static Boolean _isStarted = false;
    private static Boolean _isCapturing = false;

    private static IEventBusService _eventBus = null!;
    private static IDeviceRepositoryService _deviceRepository = null!;
    private static IMessageRepository _messageRepository = null!;
    private static ISignalHistoryRepositoryService _signalHistoryRepository = null!;
    private static ISubscriptionRepositoryService _subscriptionRepository = null!;
    private static IBleProviderService _bleProviderService = null!;
    private static IClockService _clock = null!;
    private static ConnectionService _connectionService = null!;
    private static GattBrowserService _gattBrowserService = null!;
    private static ReadWriteService _readWriteService = null!;
    private static NameResolutionService _nameResolutionService = null!;
    private static DiscoveryService _discoveryService = null!;
    private static DeviceAgingService _deviceAgingService = null!;
    private static HeartbeatService _heartbeatService = null!;
    private static SignalHistoryService _signalHistoryService = null!;
    private static IConnectionOrchestratorRegistryService _connectionOrchestratorRegistryService = null!;
    private static ILocalizationControllerService _localizationControllerService = null!;

    public static void Start()
    {
        if (_isStarted)
        {
            return;
        }
        lock (SynchronizationObject)
        {
            if (_isStarted)
            {
                return;
            }

            _eventBus = new DefaultEventBusController();
            _deviceRepository = new InMemoryDeviceRepository();
            _messageRepository = new InMemoryMessageRepository();
            _signalHistoryRepository = new InMemorySignalHistoryRepository();
            _subscriptionRepository = new InMemorySubscriptionRepository();
            _bleProviderService = new WindowsBleProvider();
            _clock = new SystemClockService();

            _connectionService = new ConnectionService(_bleProviderService, _deviceRepository, _eventBus, _messageRepository);
            _gattBrowserService = new GattBrowserService(_bleProviderService, _messageRepository);
            _readWriteService = new ReadWriteService(_bleProviderService, _messageRepository);

            _nameResolutionService = new NameResolutionService(_bleProviderService, _deviceRepository, _messageRepository, _clock);
            _discoveryService = new DiscoveryService(_bleProviderService, _deviceRepository, _eventBus, _messageRepository, _clock, _nameResolutionService);

            _deviceAgingService = new DeviceAgingService(_deviceRepository, _clock);
            _heartbeatService = new HeartbeatService(_eventBus, _connectionService, _deviceRepository, _clock);
            _signalHistoryService = new SignalHistoryService(_eventBus, _signalHistoryRepository);

            _connectionOrchestratorRegistryService = new DeviceConnectionRegistryController(_connectionService, _deviceRepository, _eventBus, _clock);
            _localizationControllerService = new GetTextLocalizationController();

            _nameResolutionService.Start();
            _signalHistoryService.Start();
            _deviceAgingService.Start();
            _heartbeatService.Start();

            _isStarted = true;
            _isCapturing = false;
        }
    }

    public static void Stop()
    {
        lock (SynchronizationObject)
        {
            if (!_isStarted)
            {
                return;
            }
            try { StopCapture(); } catch { }
            try { _heartbeatService.Stop(); } catch { }
            try { _signalHistoryService.Stop(); } catch { }
            try { _deviceAgingService.Stop(); } catch { }
            try { _nameResolutionService.Stop(); } catch { }
            _isStarted = false;
        }
    }

    public static void StartCapture()
    {
        lock (SynchronizationObject)
        {
            if (!_isStarted)
            {
                Start();
            }
            if (_isCapturing)
            {
                return;
            }
            _discoveryService.Start(ScanModeOptions.Active);
            _isCapturing = true;
        }
    }

    public static void StopCapture()
    {
        lock (SynchronizationObject)
        {
            if (!_isCapturing)
            {
                return;
            }
            try { _discoveryService.Stop(); } catch { }
            _isCapturing = false;
        }
    }

    public static Boolean IsCapturing => _isCapturing;

    public static IEventBusService EventBus => _eventBus;
    public static IDeviceRepositoryService DeviceRepository => _deviceRepository;
    public static ISignalHistoryRepositoryService SignalHistoryRepository => _signalHistoryRepository;
    public static ConnectionService ConnectionService => _connectionService;
    public static GattBrowserService GattBrowserService => _gattBrowserService;
    public static ReadWriteService ReadWriteService => _readWriteService;
    public static IConnectionOrchestratorRegistryService ConnectionOrchestratorRegistryService => _connectionOrchestratorRegistryService;
    public static ILocalizationControllerService Localization => _localizationControllerService;
    public static INameResolutionService NameResolutionService => _nameResolutionService;
    public static IMessageRepository MessageRepository => _messageRepository;
    public static IHeartbeatService HeartbeatService => _heartbeatService;
}