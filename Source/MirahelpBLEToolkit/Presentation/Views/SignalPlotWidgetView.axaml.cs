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
using MirahelpBLEToolkit.Presentation;
using System;
using System.Collections.Generic;

namespace MirahelpBLEToolkit
{
    public sealed partial class SignalPlotWidgetView : UserControl
    {
        private ISignalHistoryRepositoryService? _signalHistoryRepository;
        private IEventBusService? _eventBus;
        private IDisposable? _subscription;
        private UInt64 _deviceAddress;
        private Int32 _timeWindowSeconds;
        private String _xAxis;
        private String _yAxis;

        private Canvas _seriesHost;
        private Polyline _polyline;

        private readonly DispatcherTimer _timer;
        private Boolean _staticReady;
        private Double _lastCanvasWidth;
        private Double _lastCanvasHeight;

        public SignalPlotWidgetView()
        {
            InitializeComponent();

            _signalHistoryRepository = null;
            _eventBus = null;
            _subscription = null;
            _deviceAddress = 0;
            _timeWindowSeconds = 60;
            _xAxis = UiText(UiCatalogKeys.AxisTimeSeconds);
            _yAxis = UiText(UiCatalogKeys.AxisRssiDbm);

            _seriesHost = new Canvas();
            _seriesHost.ClipToBounds = true;

            _polyline = new Polyline();
            _polyline.Stroke = Brushes.CornflowerBlue;
            _polyline.StrokeThickness = 2;

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(33);
            _timer.Tick += OnRenderTick;

            _staticReady = false;
            _lastCanvasWidth = 0;
            _lastCanvasHeight = 0;

            SizeChanged += OnSizeChangedInternal;

            CanvasArea.PointerReleased += OnCanvasPointerReleased;
        }

        public void SetServices(ISignalHistoryRepositoryService signalHistory, IEventBusService eventBus)
        {
            _signalHistoryRepository = signalHistory;
            _eventBus = eventBus;
            IDisposable? old = _subscription;
            if (old != null)
            {
                try { old.Dispose(); } catch { }
            }
            _subscription = _eventBus.Subscribe<AdvertisementReceivedEventArgs>((AdvertisementReceivedEventArgs _) => Dispatcher.UIThread.Post(RequestFrame));
            RequestFrame();
        }

        public void SetDevice(UInt64 address)
        {
            _deviceAddress = address;
            RequestFrame();
        }

        public void SetTimeWindowSeconds(Int32 seconds)
        {
            _timeWindowSeconds = Math.Max(5, seconds);
            _staticReady = false;
            RequestFrame();
        }

        public void SetAxisLabels(String x, String y)
        {
            _xAxis = x ?? String.Empty;
            _yAxis = y ?? String.Empty;
        }

        private void OnCanvasPointerReleased(Object? sender, PointerReleasedEventArgs e)
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
            ContextMenuManager.Show(menu, CanvasArea);
        }

        private ContextMenu BuildPeriodMenu()
        {
            ContextMenu menu = new();

            MenuItem m5 = new() { Header = UiText(UiCatalogKeys.Period5s), Tag = "5" };
            m5.Click += OnPeriodMenuClick;
            menu.Items.Add(m5);

            MenuItem m15 = new() { Header = UiText(UiCatalogKeys.Period15s), Tag = "15" };
            m15.Click += OnPeriodMenuClick;
            menu.Items.Add(m15);

            MenuItem m30 = new() { Header = UiText(UiCatalogKeys.Period30s), Tag = "30" };
            m30.Click += OnPeriodMenuClick;
            menu.Items.Add(m30);

            MenuItem m60 = new() { Header = UiText(UiCatalogKeys.Period1m), Tag = "60" };
            m60.Click += OnPeriodMenuClick;
            menu.Items.Add(m60);

            MenuItem m120 = new() { Header = UiText(UiCatalogKeys.Period2m), Tag = "120" };
            m120.Click += OnPeriodMenuClick;
            menu.Items.Add(m120);

            MenuItem m300 = new() { Header = UiText(UiCatalogKeys.Period5m), Tag = "300" };
            m300.Click += OnPeriodMenuClick;
            menu.Items.Add(m300);

            MenuItem m900 = new() { Header = UiText(UiCatalogKeys.Period15m), Tag = "900" };
            m900.Click += OnPeriodMenuClick;
            menu.Items.Add(m900);

            MenuItem m1800 = new() { Header = UiText(UiCatalogKeys.Period30m), Tag = "1800" };
            m1800.Click += OnPeriodMenuClick;
            menu.Items.Add(m1800);

            MenuItem m3600 = new() { Header = UiText(UiCatalogKeys.Period60m), Tag = "3600" };
            m3600.Click += OnPeriodMenuClick;
            menu.Items.Add(m3600);

            return menu;
        }

        private void OnPeriodMenuClick(Object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            MenuItem? item = sender as MenuItem;
            if (item != null && item.Tag is String tagValue && Int32.TryParse(tagValue, out Int32 seconds))
            {
                SetTimeWindowSeconds(seconds);
            }
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

        private void OnRenderTick(Object? sender, EventArgs e)
        {
            EnsureStatic();
            if (_staticReady)
            {
                RenderFrame();
            }
        }

        private void EnsureStatic()
        {
            Double w = Math.Max(1, Bounds.Width);
            Double h = Math.Max(1, Bounds.Height);
            if (_staticReady && Math.Abs(_lastCanvasWidth - w) < 0.5 && Math.Abs(_lastCanvasHeight - h) < 0.5)
            {
                return;
            }

            _lastCanvasWidth = w;
            _lastCanvasHeight = h;

            CanvasArea.Children.Clear();
            CanvasArea.Width = w;
            CanvasArea.Height = h;

            (Double left, Double top, Double right, Double bottom) = PlotMargins();
            Double plotW = Math.Max(1, w - left - right);
            Double plotH = Math.Max(1, h - top - bottom);

            Double y50 = MapRssiToY(-50, plotH);
            Double y70 = MapRssiToY(-70, plotH);
            Double y100 = MapRssiToY(-100, plotH);

            Rectangle strong = new() { Width = plotW, Height = Math.Max(0, y50 - 0), Fill = new SolidColorBrush(Color.Parse("#3300B000")) };
            Canvas.SetLeft(strong, left);
            Canvas.SetTop(strong, top + 0);
            CanvasArea.Children.Add(strong);

            Rectangle avg = new() { Width = plotW, Height = Math.Max(0, y70 - y50), Fill = new SolidColorBrush(Color.Parse("#33FF9900")) };
            Canvas.SetLeft(avg, left);
            Canvas.SetTop(avg, top + y50);
            CanvasArea.Children.Add(avg);

            Rectangle weak = new() { Width = plotW, Height = Math.Max(0, y100 - y70), Fill = new SolidColorBrush(Color.Parse("#33E00000")) };
            Canvas.SetLeft(weak, left);
            Canvas.SetTop(weak, top + y70);
            CanvasArea.Children.Add(weak);

            _seriesHost.Width = plotW;
            _seriesHost.Height = plotH;
            _seriesHost.ClipToBounds = true;
            Canvas.SetLeft(_seriesHost, left);
            Canvas.SetTop(_seriesHost, top);

            _seriesHost.Children.Clear();
            _polyline = new Polyline();
            _polyline.Stroke = Brushes.CornflowerBlue;
            _polyline.StrokeThickness = 2;
            _seriesHost.Children.Add(_polyline);

            if (!_seriesHost.IsVisible) _seriesHost.IsVisible = true;
            CanvasArea.Children.Add(_seriesHost);

            _staticReady = true;
        }

        private void RenderFrame()
        {
            if (_signalHistoryRepository == null || _deviceAddress == 0)
            {
                _polyline.Points = new AvaloniaList<Point>();
                return;
            }

            Double plotW = _seriesHost.Width;
            Double plotH = _seriesHost.Height;

            Int32 window = Math.Max(5, _timeWindowSeconds);
            Int32 pixelWidth = Math.Max(1, (Int32)Math.Floor(plotW));
            Double secondsPerPixel = (Double)window / pixelWidth;

            DateTime now = DateTime.UtcNow;
            Double nowSeconds = (now - DateTime.UnixEpoch).TotalSeconds;
            Double leftAlignedSeconds = Math.Floor(nowSeconds / secondsPerPixel) * secondsPerPixel - window;
            DateTime leftAligned = DateTime.UnixEpoch.AddSeconds(leftAlignedSeconds);
            DateTime rightAligned = leftAligned.AddSeconds(window);

            IReadOnlyList<RssiSample> raw = _signalHistoryRepository.GetLatest(_deviceAddress, 8192);
            List<RssiSample> ordered = new(raw);
            ordered.Sort((RssiSample a, RssiSample b) => a.TimestampUtc.CompareTo(b.TimestampUtc));

            Int32 firstIn = ordered.FindIndex(s => s.TimestampUtc >= leftAligned);
            Int32 lastIn = ordered.FindLastIndex(s => s.TimestampUtc <= rightAligned);

            if (firstIn == -1 || lastIn == -1 || lastIn < firstIn)
            {
                _polyline.Points = new AvaloniaList<Point>();
                return;
            }

            Int32 prevIndex = firstIn - 1;
            Int32 nextAfterLast = lastIn + 1;

            List<(DateTime T, Double V)> points = new();

            if (prevIndex >= 0)
            {
                RssiSample a = ordered[prevIndex];
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

            AvaloniaList<Point> pts = new();
            foreach ((DateTime T, Double V) p in points)
            {
                Double x = MapTimeToX(p.T, leftAligned, rightAligned, plotW);
                if (x < 0) x = 0;
                if (x > plotW) x = plotW;
                Double y = MapRssiToY(p.V, plotH);
                pts.Add(new Point(x, y));
            }

            _polyline.Points = pts;
        }

        private static Double InterpolateY(DateTime t0, Double y0, DateTime t1, Double y1, DateTime t)
        {
            Double dt = (t1 - t0).TotalSeconds;
            if (dt <= 0.0000001) return y0;
            Double alpha = (t - t0).TotalSeconds / dt;
            Double value = y0 + (y1 - y0) * alpha;
            return value;
        }

        private static (Double Left, Double Top, Double Right, Double Bottom) PlotMargins()
        {
            return (Left: 12, Top: 12, Right: 8, Bottom: 12);
        }

        private static Double MapTimeToX(DateTime t, DateTime min, DateTime max, Double width)
        {
            Double total = (max - min).TotalSeconds;
            if (total <= 0.0001) total = 1.0;
            return ((t - min).TotalSeconds / total) * Math.Max(1, width);
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