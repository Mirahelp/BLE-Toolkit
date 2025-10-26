using MirahelpBLEToolkit.Constants;
using MirahelpBLEToolkit.Core.Enums;
using MirahelpBLEToolkit.Core.Events;
using MirahelpBLEToolkit.Core.Interfaces;
using System;
using System.Collections.Generic;
using Windows.Devices.Bluetooth.Advertisement;

namespace MirahelpBLEToolkit.Core.Platform
{
    public sealed class WindowsAdvertisementWatcher : IAdvertisementWatcherService
    {
        private readonly BluetoothLEAdvertisementWatcher _watcher;

        public event Action<AdvertisementReceivedEventArgs>? Received;

        public WindowsAdvertisementWatcher(ScanModeOptions mode)
        {
            _watcher = new BluetoothLEAdvertisementWatcher();
            _watcher.ScanningMode = mode == ScanModeOptions.Active ? BluetoothLEScanningMode.Active : BluetoothLEScanningMode.Passive;
            _watcher.Received += OnReceived;
        }

        public void Start()
        {
            _watcher.Start();
        }

        public void Stop()
        {
            _watcher.Stop();
        }

        private void OnReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            String deviceName = args.Advertisement != null && !String.IsNullOrWhiteSpace(args.Advertisement.LocalName) ? args.Advertisement.LocalName : String.Empty;

            String manufacturer = String.Empty;
            try
            {
                if (args.Advertisement != null && args.Advertisement.ManufacturerData != null && args.Advertisement.ManufacturerData.Count > 0)
                {
                    UInt16 companyId = args.Advertisement.ManufacturerData[0].CompanyId;
                    manufacturer = BuildManufacturerHex(companyId);
                }
            }
            catch
            {
                manufacturer = String.Empty;
            }

            List<Guid> serviceUuids = new();
            try
            {
                if (args.Advertisement != null && args.Advertisement.ServiceUuids != null && args.Advertisement.ServiceUuids.Count > 0)
                {
                    foreach (Guid g in args.Advertisement.ServiceUuids) serviceUuids.Add(g);
                }
            }
            catch
            {
            }

            AdvertisementTypeOptions advertisementType = MapAdvertisementType(args.AdvertisementType);

            AdvertisementReceivedEventArgs e = new()
            {
                Address = args.BluetoothAddress,
                Name = deviceName,
                Rssi = args.RawSignalStrengthInDBm,
                TimestampUtc = args.Timestamp.UtcDateTime,
                Manufacturer = manufacturer,
                ServiceUuids = serviceUuids,
                AdvertisementType = advertisementType
            };
            Action<AdvertisementReceivedEventArgs>? handler = Received;
            if (handler != null)
            {
                handler(e);
            }
        }

        private static AdvertisementTypeOptions MapAdvertisementType(BluetoothLEAdvertisementType advertisementType)
        {
            if (advertisementType == BluetoothLEAdvertisementType.ScanResponse) return AdvertisementTypeOptions.ScanResponse;
            if (advertisementType == BluetoothLEAdvertisementType.ConnectableUndirected) return AdvertisementTypeOptions.ConnectableUndirected;
            if (advertisementType == BluetoothLEAdvertisementType.ConnectableDirected) return AdvertisementTypeOptions.ConnectableDirected;
            if (advertisementType == BluetoothLEAdvertisementType.NonConnectableUndirected) return AdvertisementTypeOptions.NonConnectableUndirected;
            if (advertisementType == BluetoothLEAdvertisementType.ScannableUndirected) return AdvertisementTypeOptions.ScannableUndirected;
            if (advertisementType == BluetoothLEAdvertisementType.Extended) return AdvertisementTypeOptions.Extended;
            return AdvertisementTypeOptions.Unknown;
        }

        private static String BuildManufacturerHex(UInt16 companyId)
        {
            String hex = companyId.ToString(AppStrings.FormatHexUpper);
            if (hex.Length < 4)
            {
                hex = hex.PadLeft(4, '0');
            }
            String text = AppStrings.HexPrefix + hex;
            return text;
        }
    }
}