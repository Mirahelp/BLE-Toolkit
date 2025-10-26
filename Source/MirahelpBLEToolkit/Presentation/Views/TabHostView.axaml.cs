using Avalonia.Controls;
using MirahelpBLEToolkit.Constants;
using MirahelpBLEToolkit.Core.Interfaces;
using MirahelpBLEToolkit.Core.Models;
using MirahelpBLEToolkit.Core.Services;
using System;
using System.Collections.Generic;

namespace MirahelpBLEToolkit
{
    public sealed partial class TabHostView : UserControl
    {
        public Func<UInt64>? RequestSelectedAddress;
        public Func<IReadOnlyList<DeviceState>>? RequestAllDevices;
        public Func<IReadOnlyDictionary<UInt64, String>>? RequestAllColors;
        public Func<ReadWriteService>? RequestReadWriteServices;
        public Func<GattBrowserService>? RequestGattBrowser;
        public Func<ConnectionService>? RequestConnectionService;
        public Func<ISignalHistoryRepositoryService>? RequestSignalHistory;
        public Func<IEventBusService>? RequestEventBus;
        public Func<IConnectionOrchestratorRegistryService>? RequestConnectionOrchestrator;
        public Func<ILocalizationControllerService>? RequestLocalization;
        public Func<IMessageRepository>? RequestMessageRepository;

        private sealed class DeviceViews
        {
            public DeviceDetailsWidget Details { get; set; } = new DeviceDetailsWidget();
            public SignalPlotWidgetView Signal { get; set; } = new SignalPlotWidgetView();
            public ServicesWidget Services { get; set; } = new ServicesWidget();
            public ConnectWidget Connect { get; set; } = new ConnectWidget();
            public HeartbeatWidget Heartbeat { get; set; } = new HeartbeatWidget();
            public CommunicationWidget Communication { get; set; } = new CommunicationWidget();
            public PacketsView Packets { get; set; } = new PacketsView();
        }

        private UInt64 _selectedAddress;
        private ILocalizationControllerService? _localizationControllerService;
        private SignalPlotDashboardView _signalDashboardView = null!;
        private readonly Dictionary<UInt64, DeviceViews> _deviceViewsByAddress = new();

        public TabHostView()
        {
            InitializeComponent();
            _selectedAddress = 0;
            BtnSignal.Click += OnSignalClick;
            BtnDevice.Click += OnDeviceClick;
            ApplyLocalizedUi();
        }

        public void Initialize()
        {
            ISignalHistoryRepositoryService signalHistoryRepository = RequestSignalHistory != null ? RequestSignalHistory() : null!;
            IEventBusService eventBus = RequestEventBus != null ? RequestEventBus() : null!;
            _signalDashboardView = new SignalPlotDashboardView();
            if (signalHistoryRepository != null && eventBus != null)
            {
                IReadOnlyList<DeviceState> initialDevices = RequestAllDevices != null ? RequestAllDevices() : new List<DeviceState>();
                _signalDashboardView.SetServices(signalHistoryRepository, eventBus, initialDevices);
                _signalDashboardView.SetAxisLabels(UiText(UiCatalogKeys.AxisTimeSeconds), UiText(UiCatalogKeys.AxisRssiDbm));
            }
            SignalTile.SetTitle(UiText(UiCatalogKeys.TitleSignalStrengthAllDevices));
            SignalTile.SetContent(_signalDashboardView);
            DetailsTile.SetTitle(UiText(UiCatalogKeys.TitleDetails));
            DeviceSignalTile.SetTitle(UiText(UiCatalogKeys.TitleDeviceSignal));
            ServicesTile.SetTitle(UiText(UiCatalogKeys.TitleServicesPanel));
            ConnectTile.SetTitle(UiText(UiCatalogKeys.TitleConnect));
            HeartbeatTile.SetTitle(UiText(UiCatalogKeys.TitleHeartbeat));
            CommunicationTile.SetTitle(UiText(UiCatalogKeys.MenuCommunication));
            MessagesTile.SetTitle(UiText(UiCatalogKeys.TitleMessages));
            BtnDevice.IsEnabled = false;
        }

        public void SetLocalization(ILocalizationControllerService localizationControllerService)
        {
            _localizationControllerService = localizationControllerService;
            ApplyLocalizedUi();
        }

        public void SetSelectedAddress(UInt64 address)
        {
            _selectedAddress = address;
            Boolean hasSelection = address != 0;
            BtnDevice.IsEnabled = hasSelection;
            if (!hasSelection && DeviceTab.IsVisible)
            {
                SignalTile.IsVisible = true;
                DeviceTab.IsVisible = false;
                return;
            }
            EnsureDeviceViews(address);
            BindTilesToDevice(address);
            RefreshPerDeviceData(address);
        }

        private void EnsureDeviceViews(UInt64 address)
        {
            DeviceViews views;
            Boolean exists = _deviceViewsByAddress.TryGetValue(address, out views);
            if (exists && views != null)
            {
                return;
            }

            IMessageRepository messageRepository = RequestMessageRepository != null ? RequestMessageRepository() : null!;
            IEventBusService eventBus = RequestEventBus != null ? RequestEventBus() : null!;
            ISignalHistoryRepositoryService signalHistoryRepository = RequestSignalHistory != null ? RequestSignalHistory() : null!;
            IConnectionOrchestratorRegistryService orchestrator = RequestConnectionOrchestrator != null ? RequestConnectionOrchestrator() : null!;
            ILocalizationControllerService localization = RequestLocalization != null ? RequestLocalization() : null!;
            ConnectionService connectionService = RequestConnectionService != null ? RequestConnectionService() : null!;
            GattBrowserService gattBrowserService = RequestGattBrowser != null ? RequestGattBrowser() : null!;
            ReadWriteService readWriteService = RequestReadWriteServices != null ? RequestReadWriteServices() : null!;

            DeviceViews created = new();

            if (messageRepository != null && eventBus != null && localization != null)
            {
                created.Details.SetServices(messageRepository, eventBus, localization);
            }
            IReadOnlyList<DeviceState> devices = RequestAllDevices != null ? RequestAllDevices() : new List<DeviceState>();
            created.Details.SetDevice(address, devices);

            if (signalHistoryRepository != null && eventBus != null)
            {
                created.Signal.SetServices(signalHistoryRepository, eventBus);
                created.Signal.SetAxisLabels(UiText(UiCatalogKeys.AxisTimeSeconds), UiText(UiCatalogKeys.AxisRssiDbm));
                created.Signal.SetDevice(address);
                created.Signal.SetTimeWindowSeconds(120);
            }

            if (connectionService != null && gattBrowserService != null)
            {
                created.Services.SetServices(connectionService, gattBrowserService);
                created.Services.SetDevice(address);
            }

            if (orchestrator != null && eventBus != null && localization != null)
            {
                created.Connect.SetServices(orchestrator, eventBus, localization);
                created.Connect.SetDevice(address);
            }

            if (orchestrator != null && eventBus != null && localization != null)
            {
                created.Heartbeat.SetServices(AppHost.HeartbeatService, orchestrator, eventBus, localization);
                created.Heartbeat.SetDevice(address);
            }

            if (readWriteService != null && gattBrowserService != null && connectionService != null && localization != null)
            {
                created.Communication.SetServices(readWriteService, gattBrowserService, connectionService, localization);
                created.Communication.SetDevice(address);
            }

            if (messageRepository != null)
            {
                created.Packets.SetServices(messageRepository);
                created.Packets.SetDevice(address);
            }

            _deviceViewsByAddress[address] = created;
        }

        private void BindTilesToDevice(UInt64 address)
        {
            DeviceViews views = _deviceViewsByAddress[address];
            DetailsTile.SetContent(views.Details);
            DeviceSignalTile.SetContent(views.Signal);
            ServicesTile.SetContent(views.Services);
            ConnectTile.SetContent(views.Connect);
            HeartbeatTile.SetContent(views.Heartbeat);
            CommunicationTile.SetContent(views.Communication);
            MessagesTile.SetContent(views.Packets);
        }

        private void RefreshPerDeviceData(UInt64 address)
        {
            DeviceViews views = _deviceViewsByAddress[address];
            IReadOnlyList<DeviceState> devices = RequestAllDevices != null ? RequestAllDevices() : new List<DeviceState>();
            views.Details.SetDevice(address, devices);
        }

        private void ApplyLocalizedUi()
        {
            if (BtnSignal != null) BtnSignal.Content = UiText(UiCatalogKeys.ButtonSignal);
            if (BtnDevice != null) BtnDevice.Content = UiText(UiCatalogKeys.ButtonDevice);
        }

        private void OnSignalClick(Object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            SignalTile.IsVisible = true;
            DeviceTab.IsVisible = false;
        }

        private void OnDeviceClick(Object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (!BtnDevice.IsEnabled)
            {
                return;
            }
            SignalTile.IsVisible = false;
            DeviceTab.IsVisible = true;
            UInt64 address = RequestSelectedAddress != null ? RequestSelectedAddress() : 0;
            if (address == 0)
            {
                return;
            }
            SetSelectedAddress(address);
        }

        private String UiText(String key)
        {
            ILocalizationControllerService? localization = _localizationControllerService ?? AppHost.Localization;
            if (localization == null)
            {
                return key ?? String.Empty;
            }
            String text = localization.GetText(key);
            return text ?? String.Empty;
        }
    }
}