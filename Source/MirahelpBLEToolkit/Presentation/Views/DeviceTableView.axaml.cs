using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using MirahelpBLEToolkit.Constants;
using MirahelpBLEToolkit.Core.Controllers;
using MirahelpBLEToolkit.Core.Enums;
using MirahelpBLEToolkit.Core.Events;
using MirahelpBLEToolkit.Core.Interfaces;
using MirahelpBLEToolkit.Core.Models;
using MirahelpBLEToolkit.Presentation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace MirahelpBLEToolkit
{
    public sealed partial class DeviceTableView : UserControl
    {
        public event Action<UInt64>? DeviceSelected;

        private enum ColumnKey
        {
            Mac = 0,
            Rssi = 1,
            Status = 2,
            Name = 3,
            Manufacturer = 4
        }

        private const Double DotColumnWidth = 28.0;
        private const Double ColumnPadding = 24.0;
        private const Double HeaderFilterSpacingWidth = 10.0;
        private const Double RowHeight = 38.0;
        private const Double MeasurementSafetyPadding = 4.0;

        private readonly Dictionary<UInt64, Border> _rowByAddress;
        private UInt64 _selectedAddress;
        private IDisposable? _eventSubscription;

        private readonly Dictionary<ColumnKey, HashSet<String>> _excludedColumnFilters;

        private readonly ContextMenu _rowContextMenu;
        private readonly MenuItem _menuItemFetch;
        private UInt64 _contextAddress;
        private DateTime _lastMenuOpenedUtc;

        private IDeviceRepositoryService? _deviceRepository;
        private INameResolutionService? _nameResolutionService;
        private IEventBusService? _eventBus;
        private ILocalizationControllerService? _localizationControllerService;

        private Double[] _columnPixelWidths;

        public DeviceTableView()
        {
            InitializeComponent();

            UseLayoutRounding = true;

            _rowByAddress = new Dictionary<UInt64, Border>();
            _selectedAddress = 0;
            _excludedColumnFilters = new Dictionary<ColumnKey, HashSet<String>>();

            _rowContextMenu = new ContextMenu();
            _menuItemFetch = new MenuItem();
            _menuItemFetch.Click += OnMenuFetchClicked;
            _rowContextMenu.Items.Add(_menuItemFetch);

            _contextAddress = 0;
            _lastMenuOpenedUtc = DateTime.MinValue;

            _columnPixelWidths = new Double[8];
            _columnPixelWidths[0] = DotColumnWidth;

            BtnFilterMac.Click += (Object? s, Avalonia.Interactivity.RoutedEventArgs e) => OpenFilter(BtnFilterMac, ColumnKey.Mac, GetDistinct(ColumnKey.Mac));
            BtnFilterRssi.Click += (Object? s, Avalonia.Interactivity.RoutedEventArgs e) => OpenFilter(BtnFilterRssi, ColumnKey.Rssi, GetDistinct(ColumnKey.Rssi));
            BtnFilterStatus.Click += (Object? s, Avalonia.Interactivity.RoutedEventArgs e) => OpenFilter(BtnFilterStatus, ColumnKey.Status, GetDistinct(ColumnKey.Status));
            BtnFilterName.Click += (Object? s, Avalonia.Interactivity.RoutedEventArgs e) => OpenFilter(BtnFilterName, ColumnKey.Name, GetDistinct(ColumnKey.Name));
            BtnFilterManufacturer.Click += (Object? s, Avalonia.Interactivity.RoutedEventArgs e) => OpenFilter(BtnFilterManufacturer, ColumnKey.Manufacturer, GetDistinct(ColumnKey.Manufacturer));

            ApplyHeaderTexts();

            if (TxtHeaderMac != null) TxtHeaderMac.PointerPressed += OnHeaderMacPointerPressed;
            if (TxtHeaderRssi != null) TxtHeaderRssi.PointerPressed += OnHeaderRssiPointerPressed;
            if (TxtHeaderStatus != null) TxtHeaderStatus.PointerPressed += OnHeaderStatusPointerPressed;
            if (TxtHeaderName != null) TxtHeaderName.PointerPressed += OnHeaderNamePointerPressed;
            if (TxtHeaderManufacturer != null) TxtHeaderManufacturer.PointerPressed += OnHeaderManufacturerPointerPressed;
            if (TxtHeaderFirstSeen != null) TxtHeaderFirstSeen.PointerPressed += OnHeaderFirstPointerPressed;
            if (TxtHeaderLastSeen != null) TxtHeaderLastSeen.PointerPressed += OnHeaderLastPointerPressed;

            AddHandler(Control.ContextRequestedEvent, OnAnyContextRequested, handledEventsToo: true);
            AddHandler(InputElement.PointerPressedEvent, OnAnyPointerPressed, handledEventsToo: true);
        }

        public void SetServices(IDeviceRepositoryService deviceRepository, INameResolutionService nameResolutionService, IEventBusService eventBus, ILocalizationControllerService localizationControllerService)
        {
            _deviceRepository = deviceRepository;
            _nameResolutionService = nameResolutionService;
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
                _eventSubscription = _eventBus.Subscribe<DeviceUpsertedEventArgs>(OnDeviceUpserted);
            }

            String menuFetch = _localizationControllerService.GetText(UiCatalogKeys.MenuFetch);
            _menuItemFetch.Header = menuFetch ?? String.Empty;

            ApplyHeaderTexts();
            BuildInitial();
        }

        private void ApplyHeaderTexts()
        {
            if (TxtHeaderMac != null) TxtHeaderMac.Text = UiText(UiCatalogKeys.LabelAddress);
            if (TxtHeaderRssi != null) TxtHeaderRssi.Text = UiText(UiCatalogKeys.AxisRssiDbm);
            if (TxtHeaderStatus != null) TxtHeaderStatus.Text = UiText(UiCatalogKeys.LabelStatus);
            if (TxtHeaderName != null) TxtHeaderName.Text = UiText(UiCatalogKeys.LabelName);
            if (TxtHeaderManufacturer != null) TxtHeaderManufacturer.Text = UiText(UiCatalogKeys.LabelManufacturer);
            if (TxtHeaderFirstSeen != null) TxtHeaderFirstSeen.Text = UiText(UiCatalogKeys.LabelFirstSeen);
            if (TxtHeaderLastSeen != null) TxtHeaderLastSeen.Text = UiText(UiCatalogKeys.LabelLastSeen);
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

        private void BuildInitial()
        {
            if (_deviceRepository == null)
            {
                return;
            }
            IReadOnlyList<DeviceState> list = _deviceRepository.GetAll().OrderBy(x => x.Address).ToList();
            Rebuild(list);
        }

        private void OnDeviceUpserted(DeviceUpsertedEventArgs args)
        {
            if (args == null || _deviceRepository == null)
            {
                return;
            }
            IReadOnlyList<DeviceState> list = _deviceRepository.GetAll().OrderBy(x => x.Address).ToList();
            Avalonia.Threading.Dispatcher.UIThread.Post(() => Rebuild(list));
        }

        private IReadOnlyList<String> GetDistinct(ColumnKey columnKey)
        {
            if (_deviceRepository == null)
            {
                return new List<String>();
            }
            List<DeviceState> devices = _deviceRepository.GetAll().ToList();

            IEnumerable<String> values = Enumerable.Empty<String>();
            if (columnKey == ColumnKey.Mac)
            {
                values = devices.Select(d => NameSelectionController.FormatAddress(d.Address));
            }
            else if (columnKey == ColumnKey.Rssi)
            {
                values = devices.Select(d => d.LastRssi.HasValue ? d.LastRssi.Value.ToString() : String.Empty);
            }
            else if (columnKey == ColumnKey.Status)
            {
                values = devices.Select(d => ResolveAdvertisementTypeText(d.AdvertisementType));
            }
            else if (columnKey == ColumnKey.Name)
            {
                values = devices.Select(d => d.Name ?? String.Empty);
            }
            else if (columnKey == ColumnKey.Manufacturer)
            {
                values = devices.Select(d => d.Manufacturer ?? String.Empty);
            }

            List<String> distinct = values.Where(v => !String.IsNullOrWhiteSpace(v)).Distinct().OrderBy(v => v, StringComparer.Ordinal).ToList();
            return distinct;
        }

        private void OpenFilter(Button anchor, ColumnKey columnKey, IReadOnlyList<String> values)
        {
            FilterView view = new();

            HashSet<String> excluded;
            Boolean has = _excludedColumnFilters.TryGetValue(columnKey, out excluded);
            IReadOnlySet<String> selected = has
                ? new HashSet<String>(values.Where(v => !excluded.Contains(v)), StringComparer.OrdinalIgnoreCase)
                : new HashSet<String>(values, StringComparer.OrdinalIgnoreCase);

            view.LoadStringOptions(values, selected);
            view.StringOptionToggled += (String value, Boolean include) => UpdateColumnFilterImmediate(columnKey, value, include);

            Flyout flyout = new();
            flyout.Placement = PlacementMode.BottomEdgeAlignedLeft;
            flyout.Content = view;
            flyout.ShowAt(anchor);
        }

        private void UpdateColumnFilterImmediate(ColumnKey columnKey, String value, Boolean include)
        {
            HashSet<String> excluded;
            if (!_excludedColumnFilters.TryGetValue(columnKey, out excluded))
            {
                excluded = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
            }

            if (include)
            {
                excluded.Remove(value);
            }
            else
            {
                excluded.Add(value);
            }

            if (excluded.Count == 0)
            {
                _excludedColumnFilters.Remove(columnKey);
            }
            else
            {
                _excludedColumnFilters[columnKey] = excluded;
            }

            if (_deviceRepository == null)
            {
                return;
            }
            Rebuild(_deviceRepository.GetAll().OrderBy(x => x.Address).ToList());
        }

        private static String GetColumnValue(DeviceState deviceState, ColumnKey columnKey)
        {
            if (columnKey == ColumnKey.Mac) return NameSelectionController.FormatAddress(deviceState.Address);
            if (columnKey == ColumnKey.Rssi) return deviceState.LastRssi.HasValue ? deviceState.LastRssi.Value.ToString() : String.Empty;
            if (columnKey == ColumnKey.Status) return ResolveAdvertisementTypeDisplayStatic(deviceState.AdvertisementType);
            if (columnKey == ColumnKey.Name) return deviceState.Name ?? String.Empty;
            if (columnKey == ColumnKey.Manufacturer) return deviceState.Manufacturer ?? String.Empty;
            return String.Empty;
        }

        private Boolean IsVisibleByFilters(DeviceState deviceState)
        {
            foreach (KeyValuePair<ColumnKey, HashSet<String>> kv in _excludedColumnFilters)
            {
                ColumnKey columnKey = kv.Key;
                HashSet<String> excluded = kv.Value;
                String value = GetColumnValue(deviceState, columnKey);

                if (String.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                if (excluded.Contains(value))
                {
                    return false;
                }
            }
            return true;
        }

        private void Rebuild(IReadOnlyList<DeviceState> devices)
        {
            ComputeAndApplyColumnWidths(devices);
            RowsPanel.Children.Clear();
            _rowByAddress.Clear();
            foreach (DeviceState deviceState in devices)
            {
                if (!IsVisibleByFilters(deviceState))
                {
                    continue;
                }
                Border row = BuildRow(deviceState);
                RowsPanel.Children.Add(row);
                _rowByAddress[deviceState.Address] = row;
            }
            HighlightSelected();
        }

        private void ComputeAndApplyColumnWidths(IReadOnlyList<DeviceState> devices)
        {
            Double[] widths = new Double[8];
            for (Int32 i = 0; i < _columnPixelWidths.Length; i++)
            {
                widths[i] = _columnPixelWidths[i];
            }
            widths[0] = Math.Max(widths[0], DotColumnWidth);

            Double macHeaderWidth = MeasureControlWidth(TxtHeaderMac) + HeaderFilterSpacingWidth + MeasureControlWidth(BtnFilterMac) + ColumnPadding + MeasurementSafetyPadding;
            Double rssiHeaderWidth = MeasureControlWidth(TxtHeaderRssi) + HeaderFilterSpacingWidth + MeasureControlWidth(BtnFilterRssi) + ColumnPadding + MeasurementSafetyPadding;
            Double statusHeaderWidth = MeasureControlWidth(TxtHeaderStatus) + HeaderFilterSpacingWidth + MeasureControlWidth(BtnFilterStatus) + ColumnPadding + MeasurementSafetyPadding;
            Double nameHeaderWidth = MeasureControlWidth(TxtHeaderName) + HeaderFilterSpacingWidth + MeasureControlWidth(BtnFilterName) + ColumnPadding + MeasurementSafetyPadding;
            Double manufacturerHeaderWidth = MeasureControlWidth(TxtHeaderManufacturer) + HeaderFilterSpacingWidth + MeasureControlWidth(BtnFilterManufacturer) + ColumnPadding + MeasurementSafetyPadding;
            Double firstHeaderWidth = MeasureControlWidth(TxtHeaderFirstSeen) + ColumnPadding + MeasurementSafetyPadding;
            Double lastHeaderWidth = MeasureControlWidth(TxtHeaderLastSeen) + ColumnPadding + MeasurementSafetyPadding;

            widths[1] = Math.Max(widths[1], macHeaderWidth);
            widths[2] = Math.Max(widths[2], rssiHeaderWidth);
            widths[3] = Math.Max(widths[3], statusHeaderWidth);
            widths[4] = Math.Max(widths[4], nameHeaderWidth);
            widths[5] = Math.Max(widths[5], manufacturerHeaderWidth);
            widths[6] = Math.Max(widths[6], firstHeaderWidth);
            widths[7] = Math.Max(widths[7], lastHeaderWidth);

            foreach (DeviceState deviceState in devices)
            {
                if (!IsVisibleByFilters(deviceState))
                {
                    continue;
                }

                String valueMac = NameSelectionController.FormatAddress(deviceState.Address);
                String valueRssi = deviceState.LastRssi.HasValue ? deviceState.LastRssi.Value.ToString() : String.Empty;
                String valueStatus = ResolveAdvertisementTypeText(deviceState.AdvertisementType);
                String valueName = deviceState.Name ?? String.Empty;
                String valueManufacturer = deviceState.Manufacturer ?? String.Empty;
                String valueFirst = deviceState.FirstSeenUtc.ToLocalTime().ToString();
                String valueLast = deviceState.LastSeenUtc.ToLocalTime().ToString();

                widths[1] = Math.Max(widths[1], MeasureTextWidthUsingReference(valueMac, TxtHeaderMac, Avalonia.Media.FontWeight.Normal) + ColumnPadding + MeasurementSafetyPadding);
                widths[2] = Math.Max(widths[2], MeasureTextWidthUsingReference(valueRssi, TxtHeaderRssi, Avalonia.Media.FontWeight.Normal) + ColumnPadding + MeasurementSafetyPadding);
                widths[3] = Math.Max(widths[3], MeasureTextWidthUsingReference(valueStatus, TxtHeaderStatus, Avalonia.Media.FontWeight.Normal) + ColumnPadding + MeasurementSafetyPadding);
                widths[4] = Math.Max(widths[4], MeasureTextWidthUsingReference(valueName, TxtHeaderName, Avalonia.Media.FontWeight.Normal) + ColumnPadding + MeasurementSafetyPadding);
                widths[5] = Math.Max(widths[5], MeasureTextWidthUsingReference(valueManufacturer, TxtHeaderManufacturer, Avalonia.Media.FontWeight.Normal) + ColumnPadding + MeasurementSafetyPadding);
                widths[6] = Math.Max(widths[6], MeasureTextWidthUsingReference(valueFirst, TxtHeaderFirstSeen, Avalonia.Media.FontWeight.Normal) + ColumnPadding + MeasurementSafetyPadding);
                widths[7] = Math.Max(widths[7], MeasureTextWidthUsingReference(valueLast, TxtHeaderLastSeen, Avalonia.Media.FontWeight.Normal) + ColumnPadding + MeasurementSafetyPadding);
            }

            _columnPixelWidths = widths;

            if (HeaderGrid != null && HeaderGrid.ColumnDefinitions != null && HeaderGrid.ColumnDefinitions.Count >= 8)
            {
                HeaderGrid.ColumnDefinitions[0].Width = new GridLength(_columnPixelWidths[0], GridUnitType.Pixel);
                HeaderGrid.ColumnDefinitions[1].Width = new GridLength(_columnPixelWidths[1], GridUnitType.Pixel);
                HeaderGrid.ColumnDefinitions[2].Width = new GridLength(_columnPixelWidths[2], GridUnitType.Pixel);
                HeaderGrid.ColumnDefinitions[3].Width = new GridLength(_columnPixelWidths[3], GridUnitType.Pixel);
                HeaderGrid.ColumnDefinitions[4].Width = new GridLength(_columnPixelWidths[4], GridUnitType.Pixel);
                HeaderGrid.ColumnDefinitions[5].Width = new GridLength(_columnPixelWidths[5], GridUnitType.Pixel);
                HeaderGrid.ColumnDefinitions[6].Width = new GridLength(_columnPixelWidths[6], GridUnitType.Pixel);
                HeaderGrid.ColumnDefinitions[7].Width = new GridLength(_columnPixelWidths[7], GridUnitType.Pixel);
            }
        }

        private static Double MeasureTextWidthUsingReference(String text, TextBlock? reference, Avalonia.Media.FontWeight forcedWeight)
        {
            TextBlock textBlock = new();
            textBlock.Text = text ?? String.Empty;
            if (reference != null)
            {
                textBlock.FontFamily = reference.FontFamily;
                textBlock.FontSize = reference.FontSize;
                textBlock.FontStyle = reference.FontStyle;
                textBlock.FontStretch = reference.FontStretch;
            }
            textBlock.FontWeight = forcedWeight;
            textBlock.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));
            return textBlock.DesiredSize.Width;
        }

        private static Double MeasureControlWidth(Control? control)
        {
            if (control == null)
            {
                return 0.0;
            }
            control.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));
            return control.DesiredSize.Width;
        }

        private Border BuildRow(DeviceState deviceState)
        {
            Grid grid = new();
            grid.ColumnDefinitions.Add(new ColumnDefinition(_columnPixelWidths[0], GridUnitType.Pixel));
            grid.ColumnDefinitions.Add(new ColumnDefinition(_columnPixelWidths[1], GridUnitType.Pixel));
            grid.ColumnDefinitions.Add(new ColumnDefinition(_columnPixelWidths[2], GridUnitType.Pixel));
            grid.ColumnDefinitions.Add(new ColumnDefinition(_columnPixelWidths[3], GridUnitType.Pixel));
            grid.ColumnDefinitions.Add(new ColumnDefinition(_columnPixelWidths[4], GridUnitType.Pixel));
            grid.ColumnDefinitions.Add(new ColumnDefinition(_columnPixelWidths[5], GridUnitType.Pixel));
            grid.ColumnDefinitions.Add(new ColumnDefinition(_columnPixelWidths[6], GridUnitType.Pixel));
            grid.ColumnDefinitions.Add(new ColumnDefinition(_columnPixelWidths[7], GridUnitType.Pixel));
            grid.RowDefinitions.Add(new RowDefinition(RowHeight, GridUnitType.Pixel));

            Border cellDot = new();
            cellDot.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            cellDot.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
            cellDot.Background = Brushes.Transparent;
            cellDot.Padding = new Thickness(0.0);
            Ellipse dot = new();
            dot.Width = 13.0;
            dot.Height = 13.0;
            dot.Fill = SolidColorBrush.Parse(MirahelpBLEToolkit.Core.Models.DeviceColorGenerator.GenerateHex(deviceState.Address));
            dot.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
            dot.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
            cellDot.Child = dot;
            Grid.SetColumn(cellDot, 0);
            Grid.SetRow(cellDot, 0);
            grid.Children.Add(cellDot);

            Border cellMac = new();
            cellMac.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            cellMac.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
            cellMac.Background = Brushes.Transparent;
            cellMac.Padding = new Thickness(ColumnPadding * 0.5, 0.0, ColumnPadding * 0.5, 0.0);
            TextBlock mac = new();
            mac.Text = NameSelectionController.FormatAddress(deviceState.Address);
            mac.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
            mac.TextWrapping = TextWrapping.NoWrap;
            mac.TextTrimming = TextTrimming.None;
            cellMac.Child = mac;
            Grid.SetColumn(cellMac, 1);
            Grid.SetRow(cellMac, 0);
            grid.Children.Add(cellMac);

            Border cellRssi = new();
            cellRssi.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            cellRssi.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
            cellRssi.Background = Brushes.Transparent;
            cellRssi.Padding = new Thickness(ColumnPadding * 0.5, 0.0, ColumnPadding * 0.5, 0.0);
            TextBlock rssi = new();
            rssi.Text = deviceState.LastRssi.HasValue ? deviceState.LastRssi.Value.ToString() : String.Empty;
            rssi.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
            rssi.TextWrapping = TextWrapping.NoWrap;
            rssi.TextTrimming = TextTrimming.None;
            cellRssi.Child = rssi;
            Grid.SetColumn(cellRssi, 2);
            Grid.SetRow(cellRssi, 0);
            grid.Children.Add(cellRssi);

            Border cellStatus = new();
            cellStatus.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            cellStatus.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
            cellStatus.Background = Brushes.Transparent;
            cellStatus.Padding = new Thickness(ColumnPadding * 0.5, 0.0, ColumnPadding * 0.5, 0.0);
            TextBlock type = new();
            type.Text = ResolveAdvertisementTypeText(deviceState.AdvertisementType);
            type.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
            type.TextWrapping = TextWrapping.NoWrap;
            type.TextTrimming = TextTrimming.None;
            cellStatus.Child = type;
            Grid.SetColumn(cellStatus, 3);
            Grid.SetRow(cellStatus, 0);
            grid.Children.Add(cellStatus);

            Border cellName = new();
            cellName.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            cellName.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
            cellName.Background = Brushes.Transparent;
            cellName.Padding = new Thickness(ColumnPadding * 0.5, 0.0, ColumnPadding * 0.5, 0.0);
            TextBlock name = new();
            name.Text = deviceState.Name ?? String.Empty;
            name.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
            name.TextWrapping = TextWrapping.NoWrap;
            name.TextTrimming = TextTrimming.None;
            cellName.Child = name;
            Grid.SetColumn(cellName, 4);
            Grid.SetRow(cellName, 0);
            grid.Children.Add(cellName);

            Border cellManufacturer = new();
            cellManufacturer.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            cellManufacturer.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
            cellManufacturer.Background = Brushes.Transparent;
            cellManufacturer.Padding = new Thickness(ColumnPadding * 0.5, 0.0, ColumnPadding * 0.5, 0.0);
            TextBlock manufacturer = new();
            manufacturer.Text = deviceState.Manufacturer ?? String.Empty;
            manufacturer.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
            manufacturer.TextWrapping = TextWrapping.NoWrap;
            manufacturer.TextTrimming = TextTrimming.None;
            cellManufacturer.Child = manufacturer;
            Grid.SetColumn(cellManufacturer, 5);
            Grid.SetRow(cellManufacturer, 0);
            grid.Children.Add(cellManufacturer);

            Border cellFirst = new();
            cellFirst.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            cellFirst.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
            cellFirst.Background = Brushes.Transparent;
            cellFirst.Padding = new Thickness(ColumnPadding * 0.5, 0.0, ColumnPadding * 0.5, 0.0);
            TextBlock first = new();
            first.Text = deviceState.FirstSeenUtc.ToLocalTime().ToString();
            first.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
            first.TextWrapping = TextWrapping.NoWrap;
            first.TextTrimming = TextTrimming.None;
            cellFirst.Child = first;
            Grid.SetColumn(cellFirst, 6);
            Grid.SetRow(cellFirst, 0);
            grid.Children.Add(cellFirst);

            Border cellLast = new();
            cellLast.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            cellLast.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
            cellLast.Background = Brushes.Transparent;
            cellLast.Padding = new Thickness(ColumnPadding * 0.5, 0.0, ColumnPadding * 0.5, 0.0);
            TextBlock last = new();
            last.Text = deviceState.LastSeenUtc.ToLocalTime().ToString();
            last.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
            last.TextWrapping = TextWrapping.NoWrap;
            last.TextTrimming = TextTrimming.None;
            cellLast.Child = last;
            Grid.SetColumn(cellLast, 7);
            Grid.SetRow(cellLast, 0);
            grid.Children.Add(cellLast);

            Border row = new();
            row.Tag = deviceState.Address;
            row.Margin = new Thickness(0.0, 0.0, 0.0, 6.0);
            row.Padding = new Thickness(0.0);
            row.Height = RowHeight;
            row.Background = Brushes.Transparent;
            row.Child = grid;
            row.PointerPressed += (System.Object? s, Avalonia.Input.PointerPressedEventArgs e) => OnRowPressed(deviceState.Address);
            return row;
        }

        private static String ResolveAdvertisementTypeDisplayStatic(AdvertisementTypeOptions advertisementType)
        {
            String key = BuildAdvertisementTypeCatalogKey(advertisementType);
            return key;
        }

        private String ResolveAdvertisementTypeText(AdvertisementTypeOptions advertisementType)
        {
            String key = BuildAdvertisementTypeCatalogKey(advertisementType);
            return UiText(key);
        }

        private static String BuildAdvertisementTypeCatalogKey(AdvertisementTypeOptions advertisementType)
        {
            if (advertisementType == AdvertisementTypeOptions.ScanResponse) return UiCatalogKeys.StatusScanResponse;
            if (advertisementType == AdvertisementTypeOptions.ConnectableUndirected) return UiCatalogKeys.StatusConnectableUndirected;
            if (advertisementType == AdvertisementTypeOptions.ConnectableDirected) return UiCatalogKeys.StatusConnectableDirected;
            if (advertisementType == AdvertisementTypeOptions.NonConnectableUndirected) return UiCatalogKeys.StatusNonConnectableUndirected;
            if (advertisementType == AdvertisementTypeOptions.ScannableUndirected) return UiCatalogKeys.StatusScannableUndirected;
            if (advertisementType == AdvertisementTypeOptions.Extended) return UiCatalogKeys.StatusExtended;
            return UiCatalogKeys.TextUnknown;
        }

        private void OnRowPressed(UInt64 address)
        {
            _selectedAddress = address;
            HighlightSelected();
            Action<UInt64>? handler = DeviceSelected;
            if (handler != null)
            {
                handler(address);
            }
        }

        private static SolidColorBrush BuildAccentSelectionBrush()
        {
            Color baseAccent = Color.FromArgb(0x33, 0x00, 0x78, 0xD4);
            if (Application.Current != null)
            {
                Object? value;
                Boolean ok = Application.Current.TryFindResource("SystemAccentColor", out value);
                if (ok && value is Color ac)
                {
                    baseAccent = Color.FromArgb(0x33, ac.R, ac.G, ac.B);
                }
            }
            SolidColorBrush brush = new(baseAccent);
            return brush;
        }

        private void HighlightSelected()
        {
            SolidColorBrush selectedBrush = BuildAccentSelectionBrush();
            foreach (KeyValuePair<UInt64, Border> kv in _rowByAddress)
            {
                kv.Value.Background = kv.Key == _selectedAddress ? selectedBrush : Brushes.Transparent;
            }
        }

        private void OnMenuFetchClicked(Object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            UInt64 address = _contextAddress;
            if (address == 0 || _nameResolutionService == null)
            {
                return;
            }
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    await _nameResolutionService.FetchNowAsync(address, CancellationToken.None);
                }
                catch
                {
                }
                if (_deviceRepository == null)
                {
                    return;
                }
                IReadOnlyList<DeviceState> list = _deviceRepository.GetAll().OrderBy(x => x.Address).ToList();
                Avalonia.Threading.Dispatcher.UIThread.Post(() => Rebuild(list));
            });
        }

        private void OnAnyContextRequested(Object? sender, ContextRequestedEventArgs e)
        {
            if (DateTime.UtcNow - _lastMenuOpenedUtc < TimeSpan.FromMilliseconds(200))
            {
                return;
            }
            UInt64 address;
            Boolean ok = TryResolveAddressFromSource(e.Source, out address);
            if (!ok || address == 0)
            {
                return;
            }
            ShowRowMenu(address);
            e.Handled = true;
        }

        private void OnAnyPointerPressed(Object? sender, PointerPressedEventArgs e)
        {
            PointerPoint point = e.GetCurrentPoint(this);
            if (!point.Properties.IsRightButtonPressed)
            {
                return;
            }
            UInt64 address;
            Boolean ok = TryResolveAddressFromSource(e.Source, out address);
            if (!ok || address == 0)
            {
                return;
            }
            ShowRowMenu(address);
            e.Handled = true;
        }

        private Boolean TryResolveAddressFromSource(Object? source, out UInt64 address)
        {
            address = 0;
            Control? control = source as Control;
            while (control != null)
            {
                Border? border = control as Border;
                if (border != null && border.Tag is UInt64 a)
                {
                    address = a;
                    return true;
                }
                control = control.Parent as Control;
            }
            return false;
        }

        private void ShowRowMenu(UInt64 address)
        {
            _contextAddress = address;
            _lastMenuOpenedUtc = DateTime.UtcNow;
            ContextMenuManager.Show(_rowContextMenu, this);
        }

        private void OnHeaderMacPointerPressed(Object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            if (e != null && e.ClickCount >= 2)
            {
                ResetColumnWidthToHeader(1);
            }
        }

        private void OnHeaderRssiPointerPressed(Object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            if (e != null && e.ClickCount >= 2)
            {
                ResetColumnWidthToHeader(2);
            }
        }

        private void OnHeaderStatusPointerPressed(Object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            if (e != null && e.ClickCount >= 2)
            {
                ResetColumnWidthToHeader(3);
            }
        }

        private void OnHeaderNamePointerPressed(Object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            if (e != null && e.ClickCount >= 2)
            {
                ResetColumnWidthToHeader(4);
            }
        }

        private void OnHeaderManufacturerPointerPressed(Object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            if (e != null && e.ClickCount >= 2)
            {
                ResetColumnWidthToHeader(5);
            }
        }

        private void OnHeaderFirstPointerPressed(Object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            if (e != null && e.ClickCount >= 2)
            {
                ResetColumnWidthToHeader(6);
            }
        }

        private void OnHeaderLastPointerPressed(Object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            if (e != null && e.ClickCount >= 2)
            {
                ResetColumnWidthToHeader(7);
            }
        }

        private void ResetColumnWidthToHeader(Int32 columnIndex)
        {
            if (columnIndex == 1)
            {
                _columnPixelWidths[columnIndex] = MeasureControlWidth(TxtHeaderMac) + HeaderFilterSpacingWidth + MeasureControlWidth(BtnFilterMac) + ColumnPadding + MeasurementSafetyPadding;
            }
            else if (columnIndex == 2)
            {
                _columnPixelWidths[columnIndex] = MeasureControlWidth(TxtHeaderRssi) + HeaderFilterSpacingWidth + MeasureControlWidth(BtnFilterRssi) + ColumnPadding + MeasurementSafetyPadding;
            }
            else if (columnIndex == 3)
            {
                _columnPixelWidths[columnIndex] = MeasureControlWidth(TxtHeaderStatus) + HeaderFilterSpacingWidth + MeasureControlWidth(BtnFilterStatus) + ColumnPadding + MeasurementSafetyPadding;
            }
            else if (columnIndex == 4)
            {
                _columnPixelWidths[columnIndex] = MeasureControlWidth(TxtHeaderName) + HeaderFilterSpacingWidth + MeasureControlWidth(BtnFilterName) + ColumnPadding + MeasurementSafetyPadding;
            }
            else if (columnIndex == 5)
            {
                _columnPixelWidths[columnIndex] = MeasureControlWidth(TxtHeaderManufacturer) + HeaderFilterSpacingWidth + MeasureControlWidth(BtnFilterManufacturer) + ColumnPadding + MeasurementSafetyPadding;
            }
            else if (columnIndex == 6)
            {
                _columnPixelWidths[columnIndex] = MeasureControlWidth(TxtHeaderFirstSeen) + ColumnPadding + MeasurementSafetyPadding;
            }
            else if (columnIndex == 7)
            {
                _columnPixelWidths[columnIndex] = MeasureControlWidth(TxtHeaderLastSeen) + ColumnPadding + MeasurementSafetyPadding;
            }
            if (HeaderGrid != null && HeaderGrid.ColumnDefinitions != null && HeaderGrid.ColumnDefinitions.Count >= 8)
            {
                HeaderGrid.ColumnDefinitions[columnIndex].Width = new GridLength(_columnPixelWidths[columnIndex], GridUnitType.Pixel);
            }

            if (_deviceRepository != null)
            {
                IReadOnlyList<DeviceState> list = _deviceRepository.GetAll().OrderBy(x => x.Address).ToList();
                Rebuild(list);
            }
        }
    }
}