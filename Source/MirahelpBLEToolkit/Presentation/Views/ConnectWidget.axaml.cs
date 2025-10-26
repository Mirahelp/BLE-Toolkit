using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using MirahelpBLEToolkit.Constants;
using MirahelpBLEToolkit.Core.Enums;
using MirahelpBLEToolkit.Core.Events;
using MirahelpBLEToolkit.Core.Interfaces;
using MirahelpBLEToolkit.Core.Models;
using MirahelpBLEToolkit.Presentation;
using System;

namespace MirahelpBLEToolkit
{
    public sealed partial class ConnectWidget : UserControl
    {
        private UInt64 _address;
        private IDisposable? _eventSubscription;
        private readonly DispatcherTimer _uiTimer;
        private DeviceConnectionSnapshot _lastSnapshot;

        private IConnectionOrchestratorRegistryService? _connectionOrchestratorRegistryService;
        private IEventBusService? _eventBus;
        private ILocalizationControllerService? _localizationControllerService;

        public ConnectWidget()
        {
            InitializeComponent();

            _address = 0;
            _eventSubscription = null;
            _lastSnapshot = DefaultSnapshotFor(0);

            _uiTimer = new DispatcherTimer();
            _uiTimer.Interval = TimeSpan.FromMilliseconds(500);
            _uiTimer.Tick += OnUiTick;

            AttachedToVisualTree += OnAttachedToVisualTree;
            DetachedFromVisualTree += OnDetachedFromVisualTree;

            AddHandler(InputElement.PointerReleasedEvent, OnAnyPointerReleased, handledEventsToo: true);

            BtnToggle.Click += OnToggleClicked;

            ApplyAccentCircleStyle();
            ApplySnapshot(_lastSnapshot);
            ApplyLocalizedUi();
        }

        public void SetServices(IConnectionOrchestratorRegistryService connectionOrchestratorRegistryService, IEventBusService eventBus, ILocalizationControllerService localizationControllerService)
        {
            _connectionOrchestratorRegistryService = connectionOrchestratorRegistryService;
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
                _eventSubscription = _eventBus.Subscribe<DeviceConnectionStateChangedEventArgs>(OnConnectionStateChanged);
            }

            ApplyLocalizedUi();
        }

        public void SetDevice(UInt64 address)
        {
            _address = address;

            if (_connectionOrchestratorRegistryService != null && _address != 0)
            {
                DeviceConnectionSnapshot snapshot = _connectionOrchestratorRegistryService.GetSnapshot(_address);
                ApplySnapshot(snapshot);
            }
            else
            {
                ApplySnapshot(DefaultSnapshotFor(0));
            }
        }

        private void ApplyLocalizedUi()
        {
            if (_localizationControllerService == null)
            {
                return;
            }
            BtnToggle.Content = _localizationControllerService.GetText(UiCatalogKeys.MenuConnect);
            TxtUptimeCaption.Text = _localizationControllerService.GetText(UiCatalogKeys.TextConnected);
            TxtUptimeBig.Text = "00:00";
        }

        private static DeviceConnectionSnapshot DefaultSnapshotFor(UInt64 address)
        {
            DeviceConnectionSnapshot s = new()
            {
                Address = address,
                State = DeviceLinkStateOptions.Disconnected,
                AttemptId = Guid.Empty,
                Sequence = 0,
                BusyDepth = 0,
                AutoReconnectEnabled = false,
                ConnectedSinceUtc = DateTime.MinValue,
                NextReconnectUtc = DateTime.MinValue,
                LastError = String.Empty
            };
            return s;
        }

        private void OnAttachedToVisualTree(Object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (!_uiTimer.IsEnabled) _uiTimer.Start();
        }

        private void OnDetachedFromVisualTree(Object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (_uiTimer.IsEnabled) _uiTimer.Stop();
            IDisposable? old = _eventSubscription;
            if (old != null)
            {
                try { old.Dispose(); } catch { }
                _eventSubscription = null;
            }
        }

        private void OnUiTick(Object? sender, EventArgs e)
        {
            if (_lastSnapshot != null && _lastSnapshot.State == DeviceLinkStateOptions.Connected)
            {
                UpdateUptimeText(_lastSnapshot.ConnectedSinceUtc);
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
            DeviceConnectionSnapshot current = _lastSnapshot ?? DefaultSnapshotFor(_address);
            ContextMenu menu = BuildContextMenu(current.AutoReconnectEnabled);
            ContextMenuManager.Show(menu, this);
        }

        private ContextMenu BuildContextMenu(Boolean autoReconnectChecked)
        {
            ContextMenu menu = new();

            MenuItem autoReconnectItem = new();
            String labelAuto = _localizationControllerService != null ? _localizationControllerService.GetText(UiCatalogKeys.MenuReconnectAutomatically) : UiCatalogKeys.MenuReconnectAutomatically;
            autoReconnectItem.Header = labelAuto;
            autoReconnectItem.ToggleType = MenuItemToggleType.CheckBox;
            autoReconnectItem.IsChecked = autoReconnectChecked;
            autoReconnectItem.Click += OnAutoReconnectMenuClick;

            MenuItem refreshItem = new();
            String labelRefresh = _localizationControllerService != null ? _localizationControllerService.GetText(UiCatalogKeys.MenuFetch) : UiCatalogKeys.MenuFetch;
            refreshItem.Header = labelRefresh;
            refreshItem.Click += OnRefreshMenuClick;

            menu.Items.Add(autoReconnectItem);
            menu.Items.Add(refreshItem);
            return menu;
        }

        private void OnRefreshMenuClick(Object? sender, RoutedEventArgs e)
        {
            if (_connectionOrchestratorRegistryService == null || _address == 0)
            {
                return;
            }
            DeviceConnectionSnapshot snapshot = _connectionOrchestratorRegistryService.GetSnapshot(_address);
            ApplySnapshot(snapshot);
        }

        private void OnAutoReconnectMenuClick(Object? sender, RoutedEventArgs e)
        {
            MenuItem? item = sender as MenuItem;
            if (item == null)
            {
                return;
            }
            if (_address == 0 || _connectionOrchestratorRegistryService == null)
            {
                return;
            }
            Boolean enable = item.IsChecked;
            _connectionOrchestratorRegistryService.SetAutoReconnectEnabled(_address, enable);
        }

        private void OnConnectionStateChanged(DeviceConnectionStateChangedEventArgs e)
        {
            if (e == null || e.Snapshot == null)
            {
                return;
            }
            if (e.Snapshot.Address != _address)
            {
                return;
            }
            Dispatcher.UIThread.Post(() => ApplySnapshot(e.Snapshot));
        }

        private void ApplySnapshot(DeviceConnectionSnapshot snapshot)
        {
            _lastSnapshot = snapshot ?? DefaultSnapshotFor(_address);

            if (_localizationControllerService == null)
            {
                StatusDot.Background = Brushes.IndianRed;
                BtnToggle.IsEnabled = _address != 0 && snapshot.State != DeviceLinkStateOptions.Connecting && snapshot.State != DeviceLinkStateOptions.Disconnecting;
                UptimeCircle.IsVisible = snapshot != null && snapshot.State == DeviceLinkStateOptions.Connected;
                if (LoadingOverlay != null) LoadingOverlay.IsVisible = snapshot != null && snapshot.State == DeviceLinkStateOptions.Connecting;
                return;
            }

            if (snapshot.State == DeviceLinkStateOptions.Disconnected || snapshot.State == DeviceLinkStateOptions.Failed)
            {
                StatusDot.Background = Brushes.IndianRed;
                TxtStatus.Text = _localizationControllerService.GetText(UiCatalogKeys.TextDisconnected);
                BtnToggle.Content = _localizationControllerService.GetText(UiCatalogKeys.MenuConnect);
                BtnToggle.IsEnabled = _address != 0;
                UptimeCircle.IsVisible = false;
                if (LoadingOverlay != null) LoadingOverlay.IsVisible = false;
                return;
            }

            if (snapshot.State == DeviceLinkStateOptions.Connecting)
            {
                StatusDot.Background = Brushes.IndianRed;
                TxtStatus.Text = _localizationControllerService.GetText(UiCatalogKeys.TextConnecting);
                BtnToggle.Content = _localizationControllerService.GetText(UiCatalogKeys.MenuConnect);
                BtnToggle.IsEnabled = false;
                UptimeCircle.IsVisible = false;
                if (LoadingOverlay != null) LoadingOverlay.IsVisible = true;
                return;
            }

            if (snapshot.State == DeviceLinkStateOptions.Connected)
            {
                StatusDot.Background = Brushes.LimeGreen;
                TxtStatus.Text = _localizationControllerService.GetText(UiCatalogKeys.TextConnected);
                BtnToggle.Content = _localizationControllerService.GetText(UiCatalogKeys.MenuDisconnect);
                BtnToggle.IsEnabled = _address != 0;
                UptimeCircle.IsVisible = true;
                if (LoadingOverlay != null) LoadingOverlay.IsVisible = false;
                UpdateUptimeText(snapshot.ConnectedSinceUtc);
                return;
            }

            if (snapshot.State == DeviceLinkStateOptions.Disconnecting)
            {
                StatusDot.Background = Brushes.IndianRed;
                TxtStatus.Text = _localizationControllerService.GetText(UiCatalogKeys.TextDisconnected);
                BtnToggle.Content = _localizationControllerService.GetText(UiCatalogKeys.MenuConnect);
                BtnToggle.IsEnabled = false;
                UptimeCircle.IsVisible = false;
                if (LoadingOverlay != null) LoadingOverlay.IsVisible = false;
                return;
            }

            StatusDot.Background = Brushes.IndianRed;
            String error = String.IsNullOrWhiteSpace(snapshot.LastError) ? _localizationControllerService.GetText(UiCatalogKeys.TextDisconnected) : snapshot.LastError;
            TxtStatus.Text = error;
            BtnToggle.Content = _localizationControllerService.GetText(UiCatalogKeys.MenuConnect);
            BtnToggle.IsEnabled = _address != 0;
            UptimeCircle.IsVisible = false;
            if (LoadingOverlay != null) LoadingOverlay.IsVisible = false;
        }

        private void UpdateUptimeText(DateTime sinceUtc)
        {
            if (_localizationControllerService == null)
            {
                return;
            }
            if (sinceUtc == DateTime.MinValue)
            {
                TxtUptimeBig.Text = "00:00";
                return;
            }
            TimeSpan span = DateTime.UtcNow - sinceUtc;
            if (span.Ticks < 0) span = TimeSpan.Zero;
            Int32 hours = (Int32)span.TotalHours;
            Int32 minutes = span.Minutes;
            Int32 seconds = span.Seconds;
            String text = hours > 0 ? String.Format("{0:00}:{1:00}:{2:00}", hours, minutes, seconds) : String.Format("{0:00}:{1:00}", minutes, seconds);
            TxtUptimeBig.Text = text;
        }

        private void ApplyAccentCircleStyle()
        {
            Color accent = Color.FromArgb(0xFF, 0x00, 0x78, 0xD4);
            if (Application.Current != null)
            {
                Object? value;
                Boolean ok = Application.Current.TryFindResource("SystemAccentColor", out value);
                if (ok && value is Color ac)
                {
                    accent = ac;
                }
            }

            Color circleFill = Color.FromArgb(0x18, accent.R, accent.G, accent.B);
            Color circleStroke = Color.FromArgb(0x66, accent.R, accent.G, accent.B);

            UptimeCircle.Background = new SolidColorBrush(circleFill);
            UptimeCircle.BorderBrush = new SolidColorBrush(circleStroke);

            SolidColorBrush textBrush = new(accent);
            TxtUptimeCaption.Foreground = textBrush;
            TxtUptimeBig.Foreground = textBrush;

            StatusDot.Background = Brushes.IndianRed;
            if (_localizationControllerService != null)
            {
                TxtStatus.Text = _localizationControllerService.GetText(UiCatalogKeys.TextDisconnected);
                BtnToggle.Content = _localizationControllerService.GetText(UiCatalogKeys.MenuConnect);
            }
            BtnToggle.IsEnabled = _address != 0;
            if (LoadingOverlay != null) LoadingOverlay.IsVisible = false;
        }

        private void OnToggleClicked(Object? sender, RoutedEventArgs e)
        {
            if (_address == 0 || _connectionOrchestratorRegistryService == null)
            {
                return;
            }
            DeviceConnectionSnapshot snapshot = _connectionOrchestratorRegistryService.GetSnapshot(_address);
            if (snapshot.State == DeviceLinkStateOptions.Connected)
            {
                _connectionOrchestratorRegistryService.RequestDisconnect(_address, true);
                return;
            }

            if (_localizationControllerService != null)
            {
                TxtStatus.Text = _localizationControllerService.GetText(UiCatalogKeys.TextConnecting);
            }
            StatusDot.Background = Brushes.IndianRed;
            BtnToggle.IsEnabled = false;
            UptimeCircle.IsVisible = false;
            if (LoadingOverlay != null) LoadingOverlay.IsVisible = true;

            _connectionOrchestratorRegistryService.RequestConnect(_address);
        }
    }
}