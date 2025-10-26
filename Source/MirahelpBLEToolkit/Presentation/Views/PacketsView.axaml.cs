using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using MirahelpBLEToolkit.Constants;
using MirahelpBLEToolkit.Core.Controllers;
using MirahelpBLEToolkit.Core.Enums;
using MirahelpBLEToolkit.Core.Interfaces;
using MirahelpBLEToolkit.Core.Models;
using MirahelpBLEToolkit.Presentation;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MirahelpBLEToolkit
{
    public sealed partial class PacketsView : UserControl
    {
        private const Double ColumnPadding = 24.0;
        private const Double MeasurementSafetyPadding = 4.0;

        private UInt64 _address;
        private IMessageRepository? _messageRepository;
        private IEventBusService? _eventBus;
        private readonly DispatcherTimer _autoRefreshTimer;

        private Double[] _columnPixelWidths;

        public PacketsView()
        {
            InitializeComponent();

            _address = 0;
            _messageRepository = null;
            _eventBus = null;

            _autoRefreshTimer = new DispatcherTimer();
            _autoRefreshTimer.Interval = TimeSpan.FromMilliseconds(1000);
            _autoRefreshTimer.Tick += OnAutoRefreshTick;

            BtnBack.Click += OnBackClicked;

            AttachedToVisualTree += OnAttached;
            DetachedFromVisualTree += OnDetached;

            AddHandler(InputElement.PointerReleasedEvent, OnAnyPointerReleased, handledEventsToo: true);

            _columnPixelWidths = new Double[6];

            ApplyLocalizedUiStatic();
            ShowList();
        }

        public void SetServices(IMessageRepository messageRepository)
        {
            _messageRepository = messageRepository;
        }

        public void SetEventBus(IEventBusService eventBus)
        {
            _eventBus = eventBus;
        }

        public void SetDevice(UInt64 address)
        {
            _address = address;
            ApplyLocalizedUiStatic();
            Refresh();
            if (!_autoRefreshTimer.IsEnabled)
            {
                _autoRefreshTimer.Start();
            }
        }

        private void OnAttached(Object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (_address != 0 && !_autoRefreshTimer.IsEnabled)
            {
                _autoRefreshTimer.Start();
            }
        }

        private void OnDetached(Object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (_autoRefreshTimer.IsEnabled)
            {
                _autoRefreshTimer.Stop();
            }
        }

        private void OnAutoRefreshTick(Object? sender, EventArgs e)
        {
            if (_address == 0)
            {
                return;
            }
            Refresh();
        }

        private void OnBackClicked(Object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DetailScrollViewer.IsVisible)
            {
                ShowList();
            }
        }

        private void OnAnyPointerReleased(Object? sender, PointerReleasedEventArgs e)
        {
            if (e == null)
            {
                return;
            }
            if (e.InitialPressMouseButton != MouseButton.Right)
            {
                return;
            }

            ContextMenu contextMenu = new();

            MenuItem clearItem = new();
            clearItem.Header = UiText(UiCatalogKeys.LabelClear);
            clearItem.Click += (Object? s, Avalonia.Interactivity.RoutedEventArgs a) =>
            {
                if (_messageRepository == null || _address == 0)
                {
                    return;
                }
                _messageRepository.Clear(_address);
                Refresh();
            };
            contextMenu.Items.Add(clearItem);

            Control placementTarget = DetailScrollViewer.IsVisible ? DetailScrollViewer : ListScrollViewer;
            ContextMenuManager.Show(contextMenu, placementTarget);
        }

        private void ApplyLocalizedUiStatic()
        {
            HdrTime.Text = UiText(UiCatalogKeys.ColumnTime);
            HdrDirection.Text = UiText(UiCatalogKeys.ColumnDirection);
            HdrKind.Text = UiText(UiCatalogKeys.ColumnKind);
            HdrLength.Text = UiText(UiCatalogKeys.ColumnLength);
            HdrAddress.Text = UiText(UiCatalogKeys.LabelAddress);
            HdrHex.Text = UiText(UiCatalogKeys.LabelHex);

            LblTime.Text = UiText(UiCatalogKeys.ColumnTime);
            LblDirection.Text = UiText(UiCatalogKeys.LabelDirection);
            LblKind.Text = UiText(UiCatalogKeys.LabelKind);
            LblAddress.Text = UiText(UiCatalogKeys.LabelAddress);
            LblService.Text = UiText(UiCatalogKeys.LabelService);
            LblCharacteristic.Text = UiText(UiCatalogKeys.LabelCharacteristic);
            LblLength.Text = UiText(UiCatalogKeys.LabelLength);
            LblHex.Text = UiText(UiCatalogKeys.LabelHex);
            LblAscii.Text = UiText(UiCatalogKeys.LabelAscii);
            LblText.Text = UiText(UiCatalogKeys.LabelText);

            BtnBack.Content = UiText(UiCatalogKeys.MenuBack);
            BtnBack.IsEnabled = false;
        }

        private void Refresh()
        {
            Rows.Children.Clear();
            if (_messageRepository == null || _address == 0)
            {
                return;
            }
            MessageQuery query = new() { Address = _address, Limit = 2000 };
            List<MessageRecord> records = _messageRepository.GetLatest(_address, query).OrderBy(x => x.TimestampUtc).ToList();

            ComputeAndApplyColumnWidths(records);

            foreach (MessageRecord record in records)
            {
                Border row = BuildRow(record);
                Rows.Children.Add(row);
            }
        }

        private void ComputeAndApplyColumnWidths(IReadOnlyList<MessageRecord> records)
        {
            Double[] widths = new Double[6];
            widths[0] = Math.Max(widths[0], MeasureControlWidth(HdrTime) + ColumnPadding + MeasurementSafetyPadding);
            widths[1] = Math.Max(widths[1], MeasureControlWidth(HdrDirection) + ColumnPadding + MeasurementSafetyPadding);
            widths[2] = Math.Max(widths[2], MeasureControlWidth(HdrKind) + ColumnPadding + MeasurementSafetyPadding);
            widths[3] = Math.Max(widths[3], MeasureControlWidth(HdrLength) + ColumnPadding + MeasurementSafetyPadding);
            widths[4] = Math.Max(widths[4], MeasureControlWidth(HdrAddress) + ColumnPadding + MeasurementSafetyPadding);
            widths[5] = Math.Max(widths[5], MeasureControlWidth(HdrHex) + ColumnPadding + MeasurementSafetyPadding);

            foreach (MessageRecord record in records)
            {
                String timeValue = record.TimestampUtc.ToLocalTime().ToString("HH:mm:ss.fff");
                String directionValue = record.Direction == MessageDirectionOptions.Out ? UiText(UiCatalogKeys.LabelOut) : UiText(UiCatalogKeys.LabelIn);
                String kindValue = MapKindToText(record.Kind);
                String lengthValue = record.Data != null ? record.Data.Length.ToString() : "0";
                String addressValue = NameSelectionController.FormatAddress(record.Address);
                String hexValue = record.Data != null ? Convert.ToHexString(record.Data) : String.Empty;

                widths[0] = Math.Max(widths[0], MeasureTextWidthUsingReference(timeValue, HdrTime, Avalonia.Media.FontWeight.Normal) + ColumnPadding + MeasurementSafetyPadding);
                widths[1] = Math.Max(widths[1], MeasureTextWidthUsingReference(directionValue, HdrDirection, Avalonia.Media.FontWeight.Normal) + ColumnPadding + MeasurementSafetyPadding);
                widths[2] = Math.Max(widths[2], MeasureTextWidthUsingReference(kindValue, HdrKind, Avalonia.Media.FontWeight.Normal) + ColumnPadding + MeasurementSafetyPadding);
                widths[3] = Math.Max(widths[3], MeasureTextWidthUsingReference(lengthValue, HdrLength, Avalonia.Media.FontWeight.Normal) + ColumnPadding + MeasurementSafetyPadding);
                widths[4] = Math.Max(widths[4], MeasureTextWidthUsingReference(addressValue, HdrAddress, Avalonia.Media.FontWeight.Normal) + ColumnPadding + MeasurementSafetyPadding);
                widths[5] = Math.Max(widths[5], MeasureTextWidthUsingReference(hexValue, HdrHex, Avalonia.Media.FontWeight.Normal) + ColumnPadding + MeasurementSafetyPadding);
            }

            _columnPixelWidths = widths;

            if (HeaderGrid != null && HeaderGrid.ColumnDefinitions != null && HeaderGrid.ColumnDefinitions.Count >= 6)
            {
                HeaderGrid.ColumnDefinitions[0].Width = new GridLength(_columnPixelWidths[0], GridUnitType.Pixel);
                HeaderGrid.ColumnDefinitions[1].Width = new GridLength(_columnPixelWidths[1], GridUnitType.Pixel);
                HeaderGrid.ColumnDefinitions[2].Width = new GridLength(_columnPixelWidths[2], GridUnitType.Pixel);
                HeaderGrid.ColumnDefinitions[3].Width = new GridLength(_columnPixelWidths[3], GridUnitType.Pixel);
                HeaderGrid.ColumnDefinitions[4].Width = new GridLength(_columnPixelWidths[4], GridUnitType.Pixel);
                HeaderGrid.ColumnDefinitions[5].Width = new GridLength(_columnPixelWidths[5], GridUnitType.Pixel);
            }
        }

        private Border BuildRow(MessageRecord record)
        {
            Grid grid = new();
            grid.ColumnDefinitions.Add(new ColumnDefinition(_columnPixelWidths[0], GridUnitType.Pixel));
            grid.ColumnDefinitions.Add(new ColumnDefinition(_columnPixelWidths[1], GridUnitType.Pixel));
            grid.ColumnDefinitions.Add(new ColumnDefinition(_columnPixelWidths[2], GridUnitType.Pixel));
            grid.ColumnDefinitions.Add(new ColumnDefinition(_columnPixelWidths[3], GridUnitType.Pixel));
            grid.ColumnDefinitions.Add(new ColumnDefinition(_columnPixelWidths[4], GridUnitType.Pixel));
            grid.ColumnDefinitions.Add(new ColumnDefinition(_columnPixelWidths[5], GridUnitType.Pixel));

            Border cellTime = new();
            cellTime.Padding = new Thickness(ColumnPadding * 0.5, 0.0, ColumnPadding * 0.5, 0.0);
            TextBlock timeTextBlock = new();
            timeTextBlock.Text = record.TimestampUtc.ToLocalTime().ToString("HH:mm:ss.fff");
            timeTextBlock.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
            cellTime.Child = timeTextBlock;
            Grid.SetColumn(cellTime, 0);
            grid.Children.Add(cellTime);

            Border cellDirection = new();
            cellDirection.Padding = new Thickness(ColumnPadding * 0.5, 0.0, ColumnPadding * 0.5, 0.0);
            TextBlock directionTextBlock = new();
            directionTextBlock.Text = record.Direction == MessageDirectionOptions.Out ? UiText(UiCatalogKeys.LabelOut) : UiText(UiCatalogKeys.LabelIn);
            directionTextBlock.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
            cellDirection.Child = directionTextBlock;
            Grid.SetColumn(cellDirection, 1);
            grid.Children.Add(cellDirection);

            Border cellKind = new();
            cellKind.Padding = new Thickness(ColumnPadding * 0.5, 0.0, ColumnPadding * 0.5, 0.0);
            TextBlock kindTextBlock = new();
            kindTextBlock.Text = MapKindToText(record.Kind);
            kindTextBlock.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
            cellKind.Child = kindTextBlock;
            Grid.SetColumn(cellKind, 2);
            grid.Children.Add(cellKind);

            Border cellLen = new();
            cellLen.Padding = new Thickness(ColumnPadding * 0.5, 0.0, ColumnPadding * 0.5, 0.0);
            TextBlock lengthTextBlock = new();
            lengthTextBlock.Text = record.Data != null ? record.Data.Length.ToString() : "0";
            lengthTextBlock.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
            cellLen.Child = lengthTextBlock;
            Grid.SetColumn(cellLen, 3);
            grid.Children.Add(cellLen);

            Border cellAddress = new();
            cellAddress.Padding = new Thickness(ColumnPadding * 0.5, 0.0, ColumnPadding * 0.5, 0.0);
            TextBlock addressTextBlock = new();
            addressTextBlock.Text = NameSelectionController.FormatAddress(record.Address);
            addressTextBlock.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
            cellAddress.Child = addressTextBlock;
            Grid.SetColumn(cellAddress, 4);
            grid.Children.Add(cellAddress);

            Border cellHex = new();
            cellHex.Padding = new Thickness(ColumnPadding * 0.5, 0.0, ColumnPadding * 0.5, 0.0);
            TextBlock hexTextBlock = new();
            hexTextBlock.Text = record.Data != null ? Convert.ToHexString(record.Data) : String.Empty;
            hexTextBlock.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
            cellHex.Child = hexTextBlock;
            Grid.SetColumn(cellHex, 5);
            grid.Children.Add(cellHex);

            Border row = new();
            row.Margin = new Thickness(0.0, 0.0, 0.0, 6.0);
            row.Padding = new Thickness(0.0);
            row.Background = Avalonia.Media.Brushes.Transparent;
            row.Child = grid;
            row.Tag = record;

            row.AddHandler(InputElement.PointerReleasedEvent, (Object? s, PointerReleasedEventArgs e) =>
            {
                if (e != null && e.InitialPressMouseButton == MouseButton.Left)
                {
                    MessageRecord? r = row.Tag as MessageRecord;
                    if (r != null)
                    {
                        ShowDetails(r);
                    }
                }
            }, handledEventsToo: true);

            return row;
        }

        private void ShowDetails(MessageRecord record)
        {
            DtlTime.Text = record.TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff");
            DtlDirection.Text = record.Direction == MessageDirectionOptions.Out ? UiText(UiCatalogKeys.LabelOut) : UiText(UiCatalogKeys.LabelIn);
            DtlKind.Text = MapKindToText(record.Kind);
            DtlAddress.Text = NameSelectionController.FormatAddress(record.Address);
            DtlService.Text = record.Service.HasValue ? record.Service.Value.ToString() : String.Empty;
            DtlCharacteristic.Text = record.Characteristic.HasValue ? record.Characteristic.Value.ToString() : String.Empty;

            Int32 length = record.Data != null ? record.Data.Length : 0;
            DtlLength.Text = length.ToString();

            String hex = record.Data != null && record.Data.Length > 0 ? Convert.ToHexString(record.Data) : String.Empty;
            DtlHex.Text = hex;

            String ascii = record.Data != null && record.Data.Length > 0 ? BytesToAscii(record.Data) : String.Empty;
            DtlAscii.Text = ascii;

            DtlText.Text = record.Text ?? String.Empty;

            ShowDetail();
        }

        private void ShowList()
        {
            DetailScrollViewer.IsVisible = false;
            ListScrollViewer.IsVisible = true;
            BtnBack.IsEnabled = false;
        }

        private void ShowDetail()
        {
            ListScrollViewer.IsVisible = false;
            DetailScrollViewer.IsVisible = true;
            BtnBack.IsEnabled = true;
        }

        private static String UiText(String key)
        {
            ILocalizationControllerService localization = AppHost.Localization;
            String text = localization != null ? localization.GetText(key) : key;
            return text ?? String.Empty;
        }

        private String MapKindToText(MessageKindOptions kind)
        {
            if (kind == MessageKindOptions.ServiceQueryOut || kind == MessageKindOptions.ServiceQueryIn)
            {
                return UiText(UiCatalogKeys.TitleServices);
            }
            if (kind == MessageKindOptions.CharQueryOut || kind == MessageKindOptions.CharQueryIn)
            {
                return UiText(UiCatalogKeys.TitleCharacteristics);
            }
            if (kind == MessageKindOptions.ReadOut || kind == MessageKindOptions.ReadIn)
            {
                return UiText(UiCatalogKeys.LabelRead);
            }
            if (kind == MessageKindOptions.WriteOut)
            {
                return UiText(UiCatalogKeys.LabelWrite);
            }
            if (kind == MessageKindOptions.NotifyIn)
            {
                return UiText(UiCatalogKeys.HintResponseNotify);
            }
            if (kind == MessageKindOptions.CccdWriteOut)
            {
                return UiText(UiCatalogKeys.TitleSubscriptions);
            }
            if (kind == MessageKindOptions.OperationError)
            {
                return UiText(UiCatalogKeys.TextUnknown);
            }
            return kind.ToString();
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

        private static String BytesToAscii(Byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return String.Empty;
            Char[] chars = new Char[bytes.Length];
            for (Int32 i = 0; i < bytes.Length; i++)
            {
                Byte b = bytes[i];
                chars[i] = b >= 32 && b <= 126 ? (Char)b : '.';
            }
            return new String(chars);
        }
    }
}