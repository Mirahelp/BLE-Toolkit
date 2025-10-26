using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using MirahelpBLEToolkit.Constants;
using MirahelpBLEToolkit.Core.Events;
using MirahelpBLEToolkit.Core.Interfaces;
using MirahelpBLEToolkit.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MirahelpBLEToolkit
{
    public sealed partial class SignalPlotDashboardView : UserControl
    {
        private ISignalHistoryRepositoryService? _signalHistory;
        private IEventBusService? _eventBus;
        private IDisposable? _subscription;
        private IReadOnlySet<UInt64>? _allowedDevices;
        private Int32 _timeWindowSeconds;

        private String _axisXText;
        private String _axisYText;

        private Canvas _seriesHost;
        private readonly Dictionary<UInt64, Polyline> _polylineByAddress;

        private readonly DispatcherTimer _timer;
        private Boolean _staticReady;
        private Double _lastWidth;
        private Double _lastHeight;

        public SignalPlotDashboardView()
        {
            InitializeComponent();

            _signalHistory = null;
            _eventBus = null;
            _subscription = null;
            _allowedDevices = null;

            _timeWindowSeconds = 60;
            _axisXText = UiText(UiCatalogKeys.AxisTimeSeconds);
            _axisYText = UiText(UiCatalogKeys.AxisRssiDbm);

            _seriesHost = new Canvas();
            _seriesHost.ClipToBounds = true;

            _polylineByAddress = new Dictionary<UInt64, Polyline>();

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(33);
            _timer.Tick += OnTick;

            _staticReady = false;
            _lastWidth = 0;
            _lastHeight = 0;

            SizeChanged += OnSizeChangedInternal;

            LblPeriodCaption.Text = UiText(UiCatalogKeys.LabelPeriod);
            BtnFilterDevices.Content = UiText(UiCatalogKeys.LabelFilter);
            LblLegendStrong.Text = UiText(UiCatalogKeys.LabelStrong);
            LblLegendAverage.Text = UiText(UiCatalogKeys.LabelAverage);
            LblLegendWeak.Text = UiText(UiCatalogKeys.LabelWeak);

            CmbTimeWindow.SelectionChanged += OnTimeWindowChanged;
            BtnFilterDevices.Click += OnFilterDevicesClicked;
            PlotBorder.PointerReleased += OnPlotBorderPointerReleased;

            BuildTimeWindowOptions();
        }

        public void Configure(ISignalHistoryRepositoryService signalHistory, IEventBusService eventBus, IReadOnlyList<DeviceState> initialDevices)
        {
            _signalHistory = signalHistory;
            _eventBus = eventBus;

            IDisposable? old = _subscription;
            if (old != null)
            {
                try { old.Dispose(); } catch { }
                _subscription = null;
            }
            _subscription = _eventBus.Subscribe<AdvertisementReceivedEventArgs>((AdvertisementReceivedEventArgs _) => Dispatcher.UIThread.Post(RequestFrame));
            RequestFrame();

            ApplyInitialDevices(initialDevices);
        }

        public void SetServices(ISignalHistoryRepositoryService signalHistory, IEventBusService eventBus, IReadOnlyList<DeviceState> initialDevices)
        {
            Configure(signalHistory, eventBus, initialDevices);
        }

        public void SetAxisLabels(String x, String y)
        {
            _axisXText = x ?? String.Empty;
            _axisYText = y ?? String.Empty;
            _staticReady = false;
            RequestFrame();
        }

        private void ApplyInitialDevices(IReadOnlyList<DeviceState> initialDevices)
        {
            IReadOnlyList<DeviceState> devices = initialDevices ?? new List<DeviceState>();
            _allowedDevices = null;
            _staticReady = false;
            RequestFrame();
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
            if (item != null && item.Tag is String tagString && Int32.TryParse(tagString, out Int32 seconds))
            {
                _timeWindowSeconds = Math.Max(5, seconds);
                _staticReady = false;
                RequestFrame();
            }
        }

        private void OnFilterDevicesClicked(Object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            IReadOnlyList<DeviceState> devices = AppHost.DeviceRepository.GetAll().OrderBy(d => d.Address).ToList();
            FilterView view = new();
            view.LoadDeviceOptions(devices, _allowedDevices);
            List<UInt64> all = devices.Select(d => d.Address).ToList();
            view.DeviceOptionToggled += (UInt64 address, Boolean include) =>
            {
                HashSet<UInt64> set = _allowedDevices != null ? new HashSet<UInt64>(_allowedDevices) : new HashSet<UInt64>(all);
                if (include) set.Add(address); else set.Remove(address);
                _allowedDevices = set.Count == all.Count ? null : set;
                _staticReady = false;
                RequestFrame();
            };

            Flyout flyout = new();
            flyout.Placement = PlacementMode.BottomEdgeAlignedLeft;
            flyout.Content = view;
            flyout.ShowAt(BtnFilterDevices);
        }

        private void OnPlotBorderPointerReleased(Object? sender, PointerReleasedEventArgs e)
        {
            if (e == null)
            {
                return;
            }
            if (e.InitialPressMouseButton != MouseButton.Right)
            {
                return;
            }
            ContextMenu menu = BuildPeriodMenu();
            menu.PlacementTarget = PlotBorder;
            menu.Open(PlotBorder);
        }

        private ContextMenu BuildPeriodMenu()
        {
            ContextMenu menu = new();

            MenuItem item5 = new() { Header = UiText(UiCatalogKeys.Period5s), Tag = "5" };
            item5.Click += OnPeriodMenuClick;
            menu.Items.Add(item5);

            MenuItem item15 = new() { Header = UiText(UiCatalogKeys.Period15s), Tag = "15" };
            item15.Click += OnPeriodMenuClick;
            menu.Items.Add(item15);

            MenuItem item30 = new() { Header = UiText(UiCatalogKeys.Period30s), Tag = "30" };
            item30.Click += OnPeriodMenuClick;
            menu.Items.Add(item30);

            MenuItem item60 = new() { Header = UiText(UiCatalogKeys.Period1m), Tag = "60" };
            item60.Click += OnPeriodMenuClick;
            menu.Items.Add(item60);

            MenuItem item120 = new() { Header = UiText(UiCatalogKeys.Period2m), Tag = "120" };
            item120.Click += OnPeriodMenuClick;
            menu.Items.Add(item120);

            MenuItem item300 = new() { Header = UiText(UiCatalogKeys.Period5m), Tag = "300" };
            item300.Click += OnPeriodMenuClick;
            menu.Items.Add(item300);

            MenuItem item900 = new() { Header = UiText(UiCatalogKeys.Period15m), Tag = "900" };
            item900.Click += OnPeriodMenuClick;
            menu.Items.Add(item900);

            MenuItem item1800 = new() { Header = UiText(UiCatalogKeys.Period30m), Tag = "1800" };
            item1800.Click += OnPeriodMenuClick;
            menu.Items.Add(item1800);

            MenuItem item3600 = new() { Header = UiText(UiCatalogKeys.Period60m), Tag = "3600" };
            item3600.Click += OnPeriodMenuClick;
            menu.Items.Add(item3600);

            return menu;
        }

        private void OnPeriodMenuClick(Object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            MenuItem? item = sender as MenuItem;
            if (item != null && item.Tag is String tagString && Int32.TryParse(tagString, out Int32 seconds))
            {
                SelectTimeWindow(seconds);
            }
        }

        private void SelectTimeWindow(Int32 seconds)
        {
            Int32 target = Math.Max(5, seconds);
            foreach (Object obj in CmbTimeWindow.Items)
            {
                ComboBoxItem? it = obj as ComboBoxItem;
                if (it != null && it.Tag is String tagString && Int32.TryParse(tagString, out Int32 value) && value == target)
                {
                    CmbTimeWindow.SelectedItem = it;
                    return;
                }
            }
            _timeWindowSeconds = target;
            _staticReady = false;
            RequestFrame();
        }

        private void OnSizeChangedInternal(Object? sender, SizeChangedEventArgs e)
        {
            _staticReady = false;
            RequestFrame();
        }

        private void RequestFrame()
        {
            if (!_timer.IsEnabled) _timer.Start();
        }

        private void OnTick(Object? sender, EventArgs e)
        {
            EnsureStatic();
            if (_staticReady)
            {
                RenderFrame();
            }
        }

        private void EnsureStatic()
        {
            Double width = Math.Max(1, Bounds.Width - 16);
            Double height = Math.Max(1, Bounds.Height - 16);
            if (_staticReady && Math.Abs(width - _lastWidth) < 0.5 && Math.Abs(height - _lastHeight) < 0.5)
            {
                return;
            }

            _lastWidth = width;
            _lastHeight = height;

            CanvasArea.Children.Clear();
            CanvasArea.Width = width;
            CanvasArea.Height = height;

            (Double left, Double top, Double right, Double bottom) = PlotMargins();
            Double plotWidth = Math.Max(1, width - left - right);
            Double plotHeight = Math.Max(1, height - top - bottom);

            DrawBandsAndGrid(CanvasArea, left, top, plotWidth, plotHeight, bottom);

            _seriesHost.Width = plotWidth;
            _seriesHost.Height = plotHeight;
            _seriesHost.ClipToBounds = true;
            Canvas.SetLeft(_seriesHost, left);
            Canvas.SetTop(_seriesHost, top);

            _polylineByAddress.Clear();
            _seriesHost.Children.Clear();

            IReadOnlyList<DeviceState> devices = AppHost.DeviceRepository.GetAll();
            foreach (DeviceState device in devices)
            {
                if (_allowedDevices != null && !_allowedDevices.Contains(device.Address)) continue;

                Polyline line = new();
                line.Stroke = SolidColorBrush.Parse(DeviceColorGenerator.GenerateHex(device.Address));
                line.StrokeThickness = 2;
                _polylineByAddress[device.Address] = line;
                _seriesHost.Children.Add(line);
            }

            if (!_seriesHost.IsVisible) _seriesHost.IsVisible = true;
            CanvasArea.Children.Add(_seriesHost);

            _staticReady = true;
        }

        private void RenderFrame()
        {
            if (_signalHistory == null)
            {
                return;
            }

            Double plotWidth = _seriesHost.Width;
            Double plotHeight = _seriesHost.Height;

            Int32 window = Math.Max(5, _timeWindowSeconds);
            Int32 pixelWidth = Math.Max(1, (Int32)Math.Floor(plotWidth));
            Double secondsPerPixel = (Double)window / pixelWidth;

            DateTime now = DateTime.UtcNow;
            Double nowSeconds = (now - DateTime.UnixEpoch).TotalSeconds;
            Double leftAlignedSeconds = Math.Floor(nowSeconds / secondsPerPixel) * secondsPerPixel - window;
            DateTime leftAligned = DateTime.UnixEpoch.AddSeconds(leftAlignedSeconds);
            DateTime rightAligned = leftAligned.AddSeconds(window);

            IReadOnlyList<DeviceState> devices = AppHost.DeviceRepository.GetAll();
            HashSet<UInt64> allowed = new(devices.Where(d => _allowedDevices == null || _allowedDevices.Contains(d.Address)).Select(d => d.Address));

            List<UInt64> toRemove = new();
            foreach (UInt64 addr in _polylineByAddress.Keys)
            {
                if (!allowed.Contains(addr)) toRemove.Add(addr);
            }
            foreach (UInt64 removed in toRemove)
            {
                Polyline line;
                Boolean found = _polylineByAddress.TryGetValue(removed, out line);
                if (found)
                {
                    _seriesHost.Children.Remove(line);
                }
                _polylineByAddress.Remove(removed);
            }

            foreach (DeviceState device in devices)
            {
                if (!allowed.Contains(device.Address)) continue;

                Polyline line;
                Boolean exists = _polylineByAddress.TryGetValue(device.Address, out line);
                if (!exists)
                {
                    line = new Polyline();
                    line.Stroke = SolidColorBrush.Parse(DeviceColorGenerator.GenerateHex(device.Address));
                    line.StrokeThickness = 2;
                    _polylineByAddress[device.Address] = line;
                    _seriesHost.Children.Add(line);
                }

                IReadOnlyList<RssiSample> raw = _signalHistory.GetLatest(device.Address, 8192);
                List<RssiSample> ordered = raw.OrderBy(s => s.TimestampUtc).ToList();

                Int32 firstIn = ordered.FindIndex(s => s.TimestampUtc >= leftAligned);
                Int32 lastIn = ordered.FindLastIndex(s => s.TimestampUtc <= rightAligned);

                if (firstIn == -1 || lastIn == -1 || lastIn < firstIn)
                {
                    line.Points = new AvaloniaList<Point>();
                    continue;
                }

                Int32 previousIndex = firstIn - 1;
                Int32 nextAfterLast = lastIn + 1;

                List<(DateTime T, Double V)> points = new();

                if (previousIndex >= 0)
                {
                    RssiSample a = ordered[previousIndex];
                    RssiSample b = ordered[firstIn];
                    if (b.TimestampUtc > a.TimestampUtc)
                    {
                        Double y = InterpolateY(a.TimestampUtc, a.Rssi, b.TimestampUtc, b.Rssi, leftAligned);
                        points.Add((leftAligned, y));
                    }
                }

                for (Int32 i = firstIn; i <= lastIn; i++)
                {
                    points.Add((ordered[i].TimestampUtc, ordered[i].Rssi));
                }

                if (nextAfterLast < ordered.Count)
                {
                    RssiSample a = ordered[lastIn];
                    RssiSample b = ordered[nextAfterLast];
                    if (b.TimestampUtc > a.TimestampUtc)
                    {
                        Double y = InterpolateY(a.TimestampUtc, a.Rssi, b.TimestampUtc, b.Rssi, rightAligned);
                        points.Add((rightAligned, y));
                    }
                }

                AvaloniaList<Point> mappedPoints = new();
                foreach ((DateTime T, Double V) p in points)
                {
                    Double x = MapTimeToX(p.T, leftAligned, rightAligned, plotWidth);
                    if (x < 0) x = 0;
                    if (x > plotWidth) x = plotWidth;
                    Double y = MapRssiToY(p.V, plotHeight);
                    mappedPoints.Add(new Point(x, y));
                }

                line.Points = mappedPoints;
            }
        }

        private static Double InterpolateY(DateTime t0, Double y0, DateTime t1, Double y1, DateTime t)
        {
            Double dt = (t1 - t0).TotalSeconds;
            if (dt <= 0.0000001) return y0;
            Double alpha = (t - t0).TotalSeconds / dt;
            Double value = y0 + (y1 - y0) * alpha;
            return value;
        }

        private static Double MapRssiToY(Double rssi, Double height)
        {
            Double min = -100;
            Double max = 0;
            Double clamped = Math.Max(min, Math.Min(max, rssi));
            Double p = (clamped - max) / (min - max);
            return p * Math.Max(1, height);
        }

        private static (Double Left, Double Top, Double Right, Double Bottom) PlotMargins()
        {
            return (Left: 156, Top: 20, Right: 10, Bottom: 44);
        }

        private static Int32 ChooseTimeTickStep(Int32 window)
        {
            Int32[] candidates = new Int32[] { 1, 2, 5, 10, 15, 30, 60, 120, 300, 600, 900, 1800, 3600 };
            Int32 chosen = 1;
            foreach (Int32 candidate in candidates)
            {
                if (window % candidate != 0) continue;
                Int32 ticks = window / candidate;
                if (ticks >= 4 && ticks <= 10) chosen = candidate;
            }
            return chosen;
        }

        private static Double MapTimeToX(DateTime t, DateTime min, DateTime max, Double width)
        {
            Double total = (max - min).TotalSeconds;
            if (total <= 0.0001) total = 1.0;
            return ((t - min).TotalSeconds / total) * Math.Max(1, width);
        }

        private static String FormatDbmLabel(Int32 dbm)
        {
            if (dbm == 0) return "-0";
            return dbm.ToString();
        }

        private void DrawBandsAndGrid(Canvas canvas, Double left, Double top, Double plotWidth, Double plotHeight, Double bottom)
        {
            Double y50 = MapRssiToY(-50, plotHeight);
            Double y70 = MapRssiToY(-70, plotHeight);
            Double y100 = MapRssiToY(-100, plotHeight);

            Rectangle strong = new() { Width = plotWidth, Height = Math.Max(0, y50 - 0), Fill = new SolidColorBrush(Color.Parse("#3300B000")) };
            Canvas.SetLeft(strong, left);
            Canvas.SetTop(strong, top + 0);
            canvas.Children.Add(strong);

            Rectangle average = new() { Width = plotWidth, Height = Math.Max(0, y70 - y50), Fill = new SolidColorBrush(Color.Parse("#33FF9900")) };
            Canvas.SetLeft(average, left);
            Canvas.SetTop(average, top + y50);
            canvas.Children.Add(average);

            Rectangle weak = new() { Width = plotWidth, Height = Math.Max(0, y100 - y70), Fill = new SolidColorBrush(Color.Parse("#33E00000")) };
            Canvas.SetLeft(weak, left);
            Canvas.SetTop(weak, top + y70);
            canvas.Children.Add(weak);

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

                TextBlock label = new() { Text = FormatDbmLabel(dbm) };
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
            Int32 window = Math.Max(5, _timeWindowSeconds);
            Int32 step = ChooseTimeTickStep(window);
            Int32 tickCount = window / step;
            for (Int32 i = 0; i <= tickCount; i++)
            {
                Int32 sec = -window + (i * step);
                Double x = MapTimeToX(now.AddSeconds(sec), now.AddSeconds(-window), now, plotWidth);
                Line v = new()
                {
                    StartPoint = new Point(left + x, top),
                    EndPoint = new Point(left + x, top + plotHeight),
                    Stroke = new SolidColorBrush(Color.Parse("#30808080")),
                    StrokeThickness = 1
                };
                canvas.Children.Add(v);

                TextBlock lbl = new() { Text = sec == 0 ? "-0" : sec.ToString() };
                Double ticksY = top + plotHeight + 2;
                Canvas.SetLeft(lbl, left + x - 10);
                Canvas.SetTop(lbl, ticksY);
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

            TextBlock xAxis = new() { Text = _axisXText };
            Double xAxisY = top + plotHeight + 28;
            Canvas.SetLeft(xAxis, left + (plotWidth / 2.0) - 60.0);
            Canvas.SetTop(xAxis, xAxisY);
            canvas.Children.Add(xAxis);

            TextBlock yAxis = new() { Text = _axisYText };
            yAxis.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
            yAxis.RenderTransform = new RotateTransform(-90);
            Canvas.SetLeft(yAxis, xSeparator - 86.0);
            Canvas.SetTop(yAxis, top + (plotHeight / 2.0) - 10.0);
            canvas.Children.Add(yAxis);
        }

        private static String UiText(String key)
        {
            ILocalizationControllerService localization = AppHost.Localization;
            String text = localization != null ? localization.GetText(key) : key;
            return text ?? String.Empty;
        }
    }
}