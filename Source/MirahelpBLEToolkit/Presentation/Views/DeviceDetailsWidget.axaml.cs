using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using MirahelpBLEToolkit.Constants;
using MirahelpBLEToolkit.Core.Controllers;
using MirahelpBLEToolkit.Core.Interfaces;
using MirahelpBLEToolkit.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MirahelpBLEToolkit
{
    public sealed partial class DeviceDetailsWidget : UserControl
    {
        private UInt64 _address;
        private IDisposable? _eventSubscription;
        private IMessageRepository? _messageRepository;
        private IEventBusService? _eventBus;
        private ILocalizationControllerService? _localizationControllerService;

        public DeviceDetailsWidget()
        {
            InitializeComponent();
            _address = 0;
            _eventSubscription = null;
            ApplyLocalizedLabels();
        }

        public void SetServices(IMessageRepository messageRepository, IEventBusService eventBus, ILocalizationControllerService localizationControllerService)
        {
            _messageRepository = messageRepository;
            _eventBus = eventBus;
            _localizationControllerService = localizationControllerService;

            IDisposable? old = _eventSubscription;
            if (old != null)
            {
                try { old.Dispose(); } catch { }
                _eventSubscription = null;
            }

            if (_eventBus != null)
            {
                _eventSubscription = _eventBus.Subscribe<MirahelpBLEToolkit.Core.Events.DeviceUpsertedEventArgs>(OnDeviceUpserted);
            }

            ApplyLocalizedLabels();
        }

        public void SetDevice(UInt64 address, IReadOnlyList<DeviceState> all)
        {
            _address = address;
            DeviceState? deviceState = all.FirstOrDefault(x => x.Address == address);
            Apply(deviceState);
        }

        private void ApplyLocalizedLabels()
        {
            if (LblMac != null) LblMac.Text = UiText(UiCatalogKeys.LabelAddress);
            if (LblFirst != null) LblFirst.Text = UiText(UiCatalogKeys.LabelFirstSeen);
            if (LblLast != null) LblLast.Text = UiText(UiCatalogKeys.LabelLastSeen);
            if (LblPackets != null) LblPackets.Text = UiText(UiCatalogKeys.LabelPacketsCount);
            if (LblRssi != null) LblRssi.Text = UiText(UiCatalogKeys.LabelRssi);
            if (LblStatus != null) LblStatus.Text = UiText(UiCatalogKeys.LabelStatus);
            if (LblName != null) LblName.Text = UiText(UiCatalogKeys.LabelName);
            if (LblManufacturer != null) LblManufacturer.Text = UiText(UiCatalogKeys.LabelManufacturer);
            if (LblUuid != null) LblUuid.Text = UiText(UiCatalogKeys.LabelUuid);
            if (LblColor != null) LblColor.Text = UiText(UiCatalogKeys.LabelColor);
        }

        private String UiText(String key)
        {
            ILocalizationControllerService? svc = _localizationControllerService;
            if (svc == null)
            {
                return key ?? String.Empty;
            }
            String value = svc.GetText(key) ?? String.Empty;
            return value.Length == 0 ? (key ?? String.Empty) : value;
        }

        private void OnDeviceUpserted(MirahelpBLEToolkit.Core.Events.DeviceUpsertedEventArgs args)
        {
            if (args == null || args.Device == null)
            {
                return;
            }
            if (args.Device.Address != _address)
            {
                return;
            }
            Avalonia.Threading.Dispatcher.UIThread.Post(() => Apply(args.Device));
        }

        private void Apply(DeviceState? deviceState)
        {
            if (deviceState == null)
            {
                TxtMac.Text = String.Empty;
                TxtFirst.Text = String.Empty;
                TxtLast.Text = String.Empty;
                TxtPackets.Text = "0";
                TxtRssi.Text = String.Empty;
                TxtType.Text = String.Empty;
                TxtName.Text = String.Empty;
                TxtManufacturer.Text = String.Empty;
                TxtUuid.Text = String.Empty;
                ColorDot.Background = Brushes.Gray;
                return;
            }

            TxtMac.Text = NameSelectionController.FormatAddress(deviceState.Address);
            TxtFirst.Text = deviceState.FirstSeenUtc.ToLocalTime().ToString("dd/MMM/yyyy HH:mm:ss");
            TxtLast.Text = deviceState.LastSeenUtc.ToLocalTime().ToString("dd/MMM/yyyy HH:mm:ss");

            Int32 packetCount = 0;
            if (_messageRepository != null)
            {
                MessageQuery query = new() { Address = deviceState.Address, Limit = Int32.MaxValue };
                packetCount = _messageRepository.GetLatest(deviceState.Address, query).Count;
            }
            TxtPackets.Text = packetCount.ToString();

            TxtRssi.Text = deviceState.LastRssi.HasValue ? deviceState.LastRssi.Value.ToString() : String.Empty;
            TxtType.Text = ResolveAdvertisementTypeText(deviceState.AdvertisementType);
            TxtName.Text = deviceState.Name ?? String.Empty;
            TxtManufacturer.Text = deviceState.Manufacturer ?? String.Empty;
            TxtUuid.Text = deviceState.AdvertisedServiceUuids != null && deviceState.AdvertisedServiceUuids.Count > 0 ? deviceState.AdvertisedServiceUuids[0].ToString() : String.Empty;

            String colorHex = MirahelpBLEToolkit.Core.Models.DeviceColorGenerator.GenerateHex(deviceState.Address);
            ColorDot.Background = SolidColorBrush.Parse(colorHex);
        }

        private String ResolveAdvertisementTypeText(MirahelpBLEToolkit.Core.Enums.AdvertisementTypeOptions advertisementType)
        {
            String key = BuildAdvertisementTypeCatalogKey(advertisementType);
            return UiText(key);
        }

        private static String BuildAdvertisementTypeCatalogKey(MirahelpBLEToolkit.Core.Enums.AdvertisementTypeOptions advertisementType)
        {
            if (advertisementType == MirahelpBLEToolkit.Core.Enums.AdvertisementTypeOptions.ScanResponse) return UiCatalogKeys.StatusScanResponse;
            if (advertisementType == MirahelpBLEToolkit.Core.Enums.AdvertisementTypeOptions.ConnectableUndirected) return UiCatalogKeys.StatusConnectableUndirected;
            if (advertisementType == MirahelpBLEToolkit.Core.Enums.AdvertisementTypeOptions.ConnectableDirected) return UiCatalogKeys.StatusConnectableDirected;
            if (advertisementType == MirahelpBLEToolkit.Core.Enums.AdvertisementTypeOptions.NonConnectableUndirected) return UiCatalogKeys.StatusNonConnectableUndirected;
            if (advertisementType == MirahelpBLEToolkit.Core.Enums.AdvertisementTypeOptions.ScannableUndirected) return UiCatalogKeys.StatusScannableUndirected;
            if (advertisementType == MirahelpBLEToolkit.Core.Enums.AdvertisementTypeOptions.Extended) return UiCatalogKeys.StatusExtended;
            return UiCatalogKeys.TextUnknown;
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            IDisposable? old = _eventSubscription;
            if (old != null)
            {
                try { old.Dispose(); } catch { }
                _eventSubscription = null;
            }
        }
    }
}