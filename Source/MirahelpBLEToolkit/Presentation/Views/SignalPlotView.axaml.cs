using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Threading;
using MirahelpBLEToolkit.Constants;
using MirahelpBLEToolkit.Core.Controllers;
using MirahelpBLEToolkit.Core.Events;
using MirahelpBLEToolkit.Core.Interfaces;
using MirahelpBLEToolkit.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MirahelpBLEToolkit
{
    public sealed partial class SignalPlotView : UserControl
    {
        private ISignalHistoryRepositoryService? _signalHistoryRepository;
        private IEventBusService? _eventBus;
        private IDisposable? _subscription;
        private Boolean _allDevicesMode;
        private UInt64 _deviceAddress;
        private IReadOnlySet<UInt64>? _allowedDevices;

        private Int32 _timeWindowSeconds;

        private readonly Dictionary<UInt64, CheckBox> _filterBoxesByAddress;

        private String _xAxisText;
        private String _yAxisText;

        public SignalPlotView()
        {
            InitializeComponent();

            _signalHistoryRepository = null;
            _eventBus = null;
            _subscription = null;
            _allDevicesMode = true;
            _deviceAddress = 0;
            _allowedDevices = null;
            _timeWindowSeconds = 60;
            _filterBoxesByAddress = new Dictionary<UInt64, CheckBox>();

            CmbTimeWindow.SelectionChanged += OnTimeWindowChanged;

            BtnDeviceFilter.Click += OnDeviceFilterToggle;
            BtnDeviceFilter.Content = UiText(UiCatalogKeys.LabelFilter);
            BtnSelectAll.Click += OnSelectAll;
            BtnClearAll.Click += OnClearAll;
            BtnApplyFilter.Click += OnApplyFilter;

            SizeChanged += OnSizeChanged;

            _xAxisText = UiText(UiCatalogKeys.AxisTimeSeconds);
            _yAxisText = UiText(UiCatalogKeys.AxisRssiDbm);

            BuildTimeWindowOptions();
        }

        public void ConfigureForAllDevices(ISignalHistoryRepositoryService signalHistory, IEventBusService eventBus, IReadOnlyList<DeviceState> initialDevices)
        {
            _signalHistoryRepository = signalHistory;
            _eventBus = eventBus;
            IDisposable? old = _subscription;
            if (old != null)
            {
                try { old.Dispose(); } catch { }
            }
            _allDevicesMode = true;
            _deviceAddress = 0;
            _subscription = _eventBus.Subscribe<AdvertisementReceivedEventArgs>(OnAdvertisement);
            UpdateLegend(initialDevices);
            Invalidate();
        }

        public void ConfigureForDevice(ISignalHistoryRepositoryService signalHistory, IEventBusService eventBus)
        {
            _signalHistoryRepository = signalHistory;
            _eventBus = eventBus;
            IDisposable? old = _subscription;
            if (old != null)
            {
                try { old.Dispose(); } catch { }
            }
            _allDevicesMode = false;
            _deviceAddress = 0;
            _subscription = _eventBus.Subscribe<AdvertisementReceivedEventArgs>(OnAdvertisement);
            UpdateLegend(new List<DeviceState>());
            Invalidate();
        }

        public void SetDevice(UInt64 address)
        {
            _deviceAddress = address;
            Invalidate();
        }

        public void SetAxisLabels(String x, String y)
        {
            _xAxisText = x ?? String.Empty;
            _yAxisText = y ?? String.Empty;
            Invalidate();
        }

        public void SetAllowedDevices(IReadOnlySet<UInt64>? allowed)
        {
            _allowedDevices = allowed;
            Invalidate();
        }

        private void BuildTimeWindowOptions()
        {
            List<ComboBoxItem> items = new();
            items.Add(new ComboBoxItem { Content = UiText(UiCatalogKeys.Period5s), Tag = "5" });
            items.Add(new ComboBoxItem { Content = UiText(UiCatalogKeys.Period15s), Tag = "15" });
            items.Add(new ComboBoxItem { Content = UiText(UiCatalogKeys.Period30s), Tag = "30" });
            items.Add(new ComboBoxItem { Content = UiText(UiCatalogKeys.Period1m), Tag = "60" });
            items.Add(new ComboBoxItem { Content = UiText(UiCatalogKeys.Period2m), Tag = "120" });
            items.Add(new ComboBoxItem { Content = UiText(UiCatalogKeys.Period5m), Tag = "300" });
            items.Add(new ComboBoxItem { Content = UiText(UiCatalogKeys.Period15m), Tag = "900" });
            items.Add(new ComboBoxItem { Content = UiText(UiCatalogKeys.Period30m), Tag = "1800" });
            items.Add(new ComboBoxItem { Content = UiText(UiCatalogKeys.Period60m), Tag = "3600" });
            CmbTimeWindow.ItemsSource = items;
            CmbTimeWindow.SelectedIndex = 3;
        }

        private void OnTimeWindowChanged(Object? sender, SelectionChangedEventArgs e)
        {
            ComboBoxItem? item = CmbTimeWindow.SelectedItem as ComboBoxItem;
            if (item != null && item.Tag is String tagValue && Int32.TryParse(tagValue, out Int32 seconds))
            {
                _timeWindowSeconds = Math.Max(10, seconds);
                Invalidate();
            }
        }

        private void OnDeviceFilterToggle(Object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Boolean isVisible = FilterPanel.IsVisible;
            if (!isVisible)
            {
                BuildFilterList();
            }
            FilterPanel.IsVisible = !isVisible;
        }

        private void BuildFilterList()
        {
            FilterList.Children.Clear();
            _filterBoxesByAddress.Clear();

            IReadOnlyList<DeviceState> devices = AppHost.DeviceRepository.GetAll().OrderBy(d => d.Address).ToList();
            foreach (DeviceState device in devices)
            {
                StackPanel row = new();
                row.Orientation = Avalonia.Layout.Orientation.Horizontal;
                row.Margin = new Thickness(4, 2);

                Border dot = new();
                dot.Width = 12;
                dot.Height = 12;
                dot.CornerRadius = new CornerRadius(6);
                dot.Background = SolidColorBrush.Parse(DeviceColorGenerator.GenerateHex(device.Address));
                dot.Margin = new Thickness(0, 2, 8, 0);

                CheckBox checkBox = new();
                checkBox.IsChecked = _allowedDevices == null || _allowedDevices.Contains(device.Address);
                String deviceName = device.Name ?? String.Empty;
                String label = NameSelectionController.FormatAddress(device.Address) + (String.IsNullOrWhiteSpace(deviceName) ? String.Empty : $"  {deviceName}");
                checkBox.Content = label;

                row.Children.Add(dot);
                row.Children.Add(checkBox);
                _filterBoxesByAddress[device.Address] = checkBox;
                FilterList.Children.Add(row);
            }
        }

        private void OnSelectAll(Object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            foreach (CheckBox checkBox in _filterBoxesByAddress.Values) checkBox.IsChecked = true;
        }

        private void OnClearAll(Object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            foreach (CheckBox checkBox in _filterBoxesByAddress.Values) checkBox.IsChecked = false;
        }

        private void OnApplyFilter(Object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            HashSet<UInt64> set = new();
            foreach (KeyValuePair<UInt64, CheckBox> pair in _filterBoxesByAddress)
            {
                if (pair.Value.IsChecked.HasValue && pair.Value.IsChecked.Value) set.Add(pair.Key);
            }
            _allowedDevices = set;
            FilterPanel.IsVisible = false;
            Invalidate();
        }

        private void OnAdvertisement(AdvertisementReceivedEventArgs e)
        {
            Dispatcher.UIThread.Post(Invalidate);
        }

        private void OnSizeChanged(Object? sender, SizeChangedEventArgs e)
        {
            Invalidate();
        }

        private void UpdateLegend(IReadOnlyList<DeviceState> devices)
        {
            if (_allDevicesMode)
            {
                IReadOnlyList<DeviceState> list = devices.Count > 0 ? devices : AppHost.DeviceRepository.GetAll();
                List<String> parts = new();
                foreach (DeviceState device in list.Take(8))
                {
                    parts.Add(NameSelectionController.FormatAddress(device.Address));
                }
            }
        }

        private void Invalidate()
        {
            CanvasArea.Children.Clear();
            if (_signalHistoryRepository == null)
            {
                return;
            }

            Double width = Math.Max(1, Bounds.Width - 16);
            Double height = Math.Max(1, Bounds.Height - 16);
            CanvasArea.Width = width;
            CanvasArea.Height = height;

            DrawPlot(CanvasArea);
        }

        private static (Double Left, Double Top, Double Right, Double Bottom) PlotMargins()
        {
            return (Left: 156, Top: 20, Right: 10, Bottom: 44);
        }

        private static Int32 ChooseTimeTickStep(Int32 window)
        {
            Int32[] candidates = new Int32[] { 1, 2, 5, 10, 15, 30, 60, 120, 300, 600, 900, 1800, 3600 };
            Int32 best = 1;
            foreach (Int32 candidate in candidates)
            {
                if (window % candidate == 0)
                {
                    Int32 ticks = window / candidate;
                    if (ticks >= 4 && ticks <= 10)
                    {
                        best = candidate;
                    }
                }
            }
            return best;
        }

        private void DrawPlot(Canvas canvas)
        {
            Double width = canvas.Width;
            Double height = canvas.Height;

            (Double left, Double top, Double right, Double bottom) = PlotMargins();
            Double plotWidth = Math.Max(1, width - left - right);
            Double plotHeight = Math.Max(1, height - top - bottom);

            Rectangle backdrop = new()
            {
                Width = width,
                Height = height,
                Fill = Brushes.Transparent
            };
            canvas.Children.Add(backdrop);

            DrawBands(canvas, left, top, plotWidth, plotHeight);
            DrawGridAndAxes(canvas, left, top, plotWidth, plotHeight);

            if (_allDevicesMode)
            {
                IReadOnlyList<DeviceState> devices = AppHost.DeviceRepository.GetAll().OrderBy(d => d.Address).ToList();
                foreach (DeviceState device in devices)
                {
                    if (_allowedDevices != null && !_allowedDevices.Contains(device.Address)) continue;
                    DrawSeries(canvas, device.Address, DeviceColorGenerator.GenerateHex(device.Address), left, top, plotWidth, plotHeight);
                }
            }
            else if (_deviceAddress != 0)
            {
                DrawSeries(canvas, _deviceAddress, DeviceColorGenerator.GenerateHex(_deviceAddress), left, top, plotWidth, plotHeight);
            }
        }

        private void DrawBands(Canvas canvas, Double left, Double top, Double plotWidth, Double plotHeight)
        {
            Double y50 = MapRssiToY(-50, plotHeight);
            Double y70 = MapRssiToY(-70, plotHeight);
            Double y100 = MapRssiToY(-100, plotHeight);

            Rectangle strong = new()
            {
                Width = plotWidth,
                Height = Math.Max(0, y50 - 0),
                Fill = new SolidColorBrush(Color.Parse("#3300B000"))
            };
            Canvas.SetLeft(strong, left);
            Canvas.SetTop(strong, top + 0);
            canvas.Children.Add(strong);

            Rectangle average = new()
            {
                Width = plotWidth,
                Height = Math.Max(0, y70 - y50),
                Fill = new SolidColorBrush(Color.Parse("#33FF9900"))
            };
            Canvas.SetLeft(average, left);
            Canvas.SetTop(average, top + y50);
            canvas.Children.Add(average);

            Rectangle weak = new()
            {
                Width = plotWidth,
                Height = Math.Max(0, y100 - y70),
                Fill = new SolidColorBrush(Color.Parse("#33E00000"))
            };
            Canvas.SetLeft(weak, left);
            Canvas.SetTop(weak, top + y70);
            canvas.Children.Add(weak);
        }

        private void DrawGridAndAxes(Canvas canvas, Double left, Double top, Double plotWidth, Double plotHeight)
        {
            Double yLabelAreaWidth = 48;
            Double yAxisSeparatorGap = 8;
            Double xNumbersStart = left - yLabelAreaWidth;
            Double xSeparator = xNumbersStart - yAxisSeparatorGap;

            for (Int32 dbm = 0; dbm >= -100; dbm -= 20)
            {
                Double y = MapRssiToY(dbm, plotHeight);
                Line line = new()
                {
                    StartPoint = new Point(left, top + y),
                    EndPoint = new Point(left + plotWidth, top + y),
                    Stroke = new SolidColorBrush(Color.Parse("#30808080")),
                    StrokeThickness = 1
                };
                canvas.Children.Add(line);

                TextBlock label = new();
                label.Text = dbm.ToString();
                Canvas.SetLeft(label, xNumbersStart + 2);
                Canvas.SetTop(label, Math.Max(0, top + y - 9));
                canvas.Children.Add(label);
            }

            Line ySeparator = new()
            {
                StartPoint = new Point(xSeparator, top),
                EndPoint = new Point(xSeparator, top + plotHeight),
                Stroke = new SolidColorBrush(Color.Parse("#30808080")),
                StrokeThickness = 1
            };
            canvas.Children.Add(ySeparator);

            DateTime now = DateTime.UtcNow;
            Int32 window = Math.Max(10, _timeWindowSeconds);
            Int32 step = ChooseTimeTickStep(window);

            for (Int32 sec = -window; sec <= 0; sec += step)
            {
                Double x = MapTimeToX(now.AddSeconds(sec), now.AddSeconds(-window), now, plotWidth);
                Line v = new()
                {
                    StartPoint = new Point(left + x, top),
                    EndPoint = new Point(left + x, top + plotHeight),
                    Stroke = new SolidColorBrush(Color.Parse("#30808080")),
                    StrokeThickness = 1
                };
                canvas.Children.Add(v);

                TextBlock lbl = new();
                lbl.Text = sec.ToString();
                Double yLabel = top + plotHeight + Math.Max(6, 44 - 14);
                Canvas.SetLeft(lbl, left + x - 10);
                Canvas.SetTop(lbl, yLabel);
                canvas.Children.Add(lbl);
            }

            Rectangle frame = new()
            {
                Width = plotWidth,
                Height = plotHeight,
                Stroke = new SolidColorBrush(Color.Parse("#40808080")),
                StrokeThickness = 1,
                Fill = Brushes.Transparent
            };
            Canvas.SetLeft(frame, left);
            Canvas.SetTop(frame, top);
            canvas.Children.Add(frame);

            TextBlock xAxis = new() { Text = _xAxisText };
            Canvas.SetLeft(xAxis, left + (plotWidth / 2) - 60);
            Canvas.SetTop(xAxis, top + plotHeight + 28);
            canvas.Children.Add(xAxis);

            TextBlock yAxis = new() { Text = _yAxisText };
            yAxis.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
            yAxis.RenderTransform = new RotateTransform(-90);
            Canvas.SetLeft(yAxis, xSeparator - 86);
            Canvas.SetTop(yAxis, top + (plotHeight / 2) - 10);
            canvas.Children.Add(yAxis);
        }

        private void DrawSeries(Canvas canvas, UInt64 address, String colorHex, Double left, Double top, Double plotWidth, Double plotHeight)
        {
            ISignalHistoryRepositoryService repository = _signalHistoryRepository!;
            List<RssiSample> samples = repository.GetLatest(address, 4096).ToList();
            if (samples.Count < 2)
            {
                return;
            }

            DateTime now = DateTime.UtcNow;
            DateTime min = now.AddSeconds(-_timeWindowSeconds);

            List<RssiSample> window = samples.Where(s => s.TimestampUtc >= min && s.TimestampUtc <= now)
                                             .OrderBy(s => s.TimestampUtc)
                                             .ToList();
            if (window.Count < 2)
            {
                return;
            }

            AvaloniaList<Point> points = new();
            foreach (RssiSample sample in window)
            {
                Double x = MapTimeToX(sample.TimestampUtc, min, now, plotWidth);
                Double y = MapRssiToY(sample.Rssi, plotHeight);
                points.Add(new Point(left + x, top + y));
            }

            Polyline polyline = new()
            {
                Points = points,
                Stroke = SolidColorBrush.Parse(colorHex),
                StrokeThickness = 2
            };
            canvas.Children.Add(polyline);
        }

        private static Double MapTimeToX(DateTime t, DateTime min, DateTime max, Double width)
        {
            Double total = (max - min).TotalSeconds;
            if (total <= 0.0001) total = 1.0;
            Double x = (t - min).TotalSeconds / total;
            return x * Math.Max(1, width);
        }

        private static Double MapRssiToY(Double rssi, Double height)
        {
            Double min = -100;
            Double max = 0;
            Double clamped = Math.Max(min, Math.Min(max, rssi));
            Double p = (clamped - max) / (min - max);
            return p * Math.Max(1, height);
        }

        private static String UiText(String key)
        {
            ILocalizationControllerService localization = AppHost.Localization;
            String text = localization != null ? localization.GetText(key) : key;
            return text ?? String.Empty;
        }
    }
}