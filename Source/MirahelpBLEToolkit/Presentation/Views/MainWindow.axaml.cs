using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using MirahelpBLEToolkit.Constants;
using MirahelpBLEToolkit.Core.Interfaces;
using MirahelpBLEToolkit.Core.Models;
using MirahelpBLEToolkit.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MirahelpBLEToolkit
{
    public sealed partial class MainWindow : Window
    {
        private UInt64 _selectedAddress;

        public MainWindow()
        {
            InitializeComponent();

            _selectedAddress = 0;

            DeviceTable.SetServices(AppHost.DeviceRepository, AppHost.NameResolutionService, AppHost.EventBus, AppHost.Localization);
            DeviceTable.DeviceSelected += OnDeviceSelected;

            Tabs.RequestSelectedAddress += OnRequestSelectedAddress;
            Tabs.RequestAllDevices += OnRequestAllDevices;
            Tabs.RequestAllColors += OnRequestAllColors;
            Tabs.RequestReadWriteServices += OnRequestReadWriteServices;
            Tabs.RequestGattBrowser += OnRequestGattBrowser;
            Tabs.RequestConnectionService += OnRequestConnectionService;
            Tabs.RequestSignalHistory += OnRequestSignalHistory;
            Tabs.RequestEventBus += OnRequestEventBus;
            Tabs.RequestConnectionOrchestrator += OnRequestConnectionOrchestrator;
            Tabs.RequestLocalization += OnRequestLocalization;
            Tabs.RequestMessageRepository += OnRequestMessageRepository;
            Tabs.Initialize();

            BtnCapture.Click += OnCaptureClicked;
            UpdateCaptureButton();

            if (BtnThemeToggle != null)
            {
                BtnThemeToggle.Click += OnThemeToggleClicked;
                UpdateThemeToggleButton();
            }
        }

        private void ApplyLogoForTheme()
        {
            if (AppLogo == null)
            {
                return;
            }
            ThemeVariant current = Application.Current != null ? Application.Current.ActualThemeVariant : ThemeVariant.Dark;
            String uriString = current == ThemeVariant.Dark
                ? "avares://MirahelpBLEToolkit/Assets/Images/Mirahelp_light_logo.png"
                : "avares://MirahelpBLEToolkit/Assets/Images/Mirahelp_dark_logo.png";
            Uri assetUri = new(uriString);
            System.IO.Stream stream = AssetLoader.Open(assetUri);
            try
            {
                Bitmap bitmap = new(stream);
                AppLogo.Source = bitmap;
            }
            finally
            {
                try { stream.Dispose(); } catch { }
            }
        }

        private void OnThemeToggleClicked(Object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            ThemeVariant current = Application.Current != null ? Application.Current.ActualThemeVariant : ThemeVariant.Dark;
            ThemeVariant next = current == ThemeVariant.Dark ? ThemeVariant.Light : ThemeVariant.Dark;
            if (Application.Current != null)
            {
                Application.Current.RequestedThemeVariant = next;
            }
            UpdateThemeToggleButton();
        }

        private void UpdateThemeToggleButton()
        {
            if (BtnThemeToggle == null)
            {
                return;
            }
            ThemeVariant current = Application.Current != null ? Application.Current.ActualThemeVariant : ThemeVariant.Dark;
            BtnThemeToggle.Content = current == ThemeVariant.Dark ? UiText(UiCatalogKeys.ButtonThemeDark) : UiText(UiCatalogKeys.ButtonThemeLight);
            ApplyLogoForTheme();
        }

        private void OnCaptureClicked(Object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (AppHost.IsCapturing)
            {
                AppHost.StopCapture();
            }
            else
            {
                AppHost.StartCapture();
            }
            UpdateCaptureButton();
        }

        private void UpdateCaptureButton()
        {
            BtnCapture.Content = AppHost.IsCapturing ? UiText(UiCatalogKeys.ButtonStopCapture) : UiText(UiCatalogKeys.ButtonStartCapture);
        }

        private void OnDeviceSelected(UInt64 address)
        {
            _selectedAddress = address;
            Tabs.SetSelectedAddress(address);
        }

        private UInt64 OnRequestSelectedAddress()
        {
            return _selectedAddress;
        }

        private IReadOnlyList<DeviceState> OnRequestAllDevices()
        {
            IReadOnlyList<DeviceState> list = AppHost.DeviceRepository.GetAll().OrderBy(d => d.Address).ToList();
            return list;
        }

        private IReadOnlyDictionary<UInt64, String> OnRequestAllColors()
        {
            Dictionary<UInt64, String> map = new();
            foreach (DeviceState deviceState in AppHost.DeviceRepository.GetAll())
            {
                map[deviceState.Address] = DeviceColorGenerator.GenerateHex(deviceState.Address);
            }
            return map;
        }

        private ReadWriteService OnRequestReadWriteServices()
        {
            return AppHost.ReadWriteService;
        }

        private GattBrowserService OnRequestGattBrowser()
        {
            return AppHost.GattBrowserService;
        }

        private ConnectionService OnRequestConnectionService()
        {
            return AppHost.ConnectionService;
        }

        private ISignalHistoryRepositoryService OnRequestSignalHistory()
        {
            return AppHost.SignalHistoryRepository;
        }

        private IEventBusService OnRequestEventBus()
        {
            return AppHost.EventBus;
        }

        private IConnectionOrchestratorRegistryService OnRequestConnectionOrchestrator()
        {
            return AppHost.ConnectionOrchestratorRegistryService;
        }

        private ILocalizationControllerService OnRequestLocalization()
        {
            return AppHost.Localization;
        }

        private IMessageRepository OnRequestMessageRepository()
        {
            return AppHost.MessageRepository;
        }

        private static String UiText(String key)
        {
            ILocalizationControllerService localization = AppHost.Localization;
            String text = localization != null ? localization.GetText(key) : key;
            return text ?? String.Empty;
        }
    }
}