using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using MirahelpBLEToolkit.Constants;
using MirahelpBLEToolkit.Core.Enums;
using MirahelpBLEToolkit.Core.Events;
using MirahelpBLEToolkit.Core.Interfaces;
using MirahelpBLEToolkit.Core.Models;
using MirahelpBLEToolkit.Presentation;
using System;
using System.Threading;

namespace MirahelpBLEToolkit
{
    public sealed partial class HeartbeatWidget : UserControl
    {
        private UInt64 _address;
        private IDisposable? _heartbeatSubscription;
        private IDisposable? _linkSubscription;
        private readonly DispatcherTimer _countdownTimer;
        private DeviceHeartbeatSnapshot _lastSnapshot;
        private Boolean _isProbeKickInProgress;

        private IHeartbeatService? _heartbeatService;
        private IConnectionOrchestratorRegistryService? _connectionOrchestratorRegistryService;
        private IEventBusService? _eventBus;
        private ILocalizationControllerService? _localizationControllerService;

        private CancellationTokenSource _lifetimeCancellationTokenSource;

        public HeartbeatWidget()
        {
            InitializeComponent();
            _address = 0;
            _heartbeatSubscription = null;
            _linkSubscription = null;
            _lastSnapshot = new DeviceHeartbeatSnapshot();
            _isProbeKickInProgress = false;

            _lifetimeCancellationTokenSource = new CancellationTokenSource();

            _countdownTimer = new DispatcherTimer();
            _countdownTimer.Interval = TimeSpan.FromSeconds(1);
            _countdownTimer.Tick += OnCountdownTick;

            AddHandler(InputElement.PointerReleasedEvent, OnAnyPointerReleased, handledEventsToo: true);
        }

        public void SetServices(IHeartbeatService heartbeatService, IConnectionOrchestratorRegistryService connectionOrchestratorRegistryService, IEventBusService eventBus, ILocalizationControllerService localizationControllerService)
        {
            _heartbeatService = heartbeatService;
            _connectionOrchestratorRegistryService = connectionOrchestratorRegistryService;
            _eventBus = eventBus;
            _localizationControllerService = localizationControllerService;

            IDisposable? oldHb = _heartbeatSubscription;
            if (oldHb != null)
            {
                try { oldHb.Dispose(); } catch { }
                _heartbeatSubscription = null;
            }
            IDisposable? oldLs = _linkSubscription;
            if (oldLs != null)
            {
                try { oldLs.Dispose(); } catch { }
                _linkSubscription = null;
            }

            if (_eventBus != null)
            {
                _heartbeatSubscription = _eventBus.Subscribe<HeartbeatStateChangedEventArgs>(OnHeartbeatStateChanged);
                _linkSubscription = _eventBus.Subscribe<DeviceLinkStatusChangedEventArgs>(OnLinkStatusChanged);
            }
        }

        public void SetDevice(UInt64 address)
        {
            _address = address;

            CancellationTokenSource previous = _lifetimeCancellationTokenSource;
            try { previous.Cancel(); } catch { }
            try { previous.Dispose(); } catch { }
            _lifetimeCancellationTokenSource = new CancellationTokenSource();

            if (_heartbeatService == null)
            {
                ApplySnapshot(new DeviceHeartbeatSnapshot { Address = _address });
                return;
            }

            _heartbeatService.SetEnabled(_address, true);
            _heartbeatService.SetPeriod(_address, 10);
            DeviceHeartbeatSnapshot snapshot = _heartbeatService.GetSnapshot(_address);
            ApplySnapshot(snapshot);

            if (!_countdownTimer.IsEnabled)
            {
                _countdownTimer.Start();
            }

            if (_connectionOrchestratorRegistryService != null)
            {
                DeviceConnectionSnapshot link = _connectionOrchestratorRegistryService.GetSnapshot(_address);
                Boolean isConnected = link.State == DeviceLinkStateOptions.Connected;
                if (isConnected && !_isProbeKickInProgress)
                {
                    _ = KickProbeNowAsync(_lifetimeCancellationTokenSource.Token);
                }
            }
        }

        private async System.Threading.Tasks.Task KickProbeNowAsync(CancellationToken cancellationToken)
        {
            if (_heartbeatService == null)
            {
                return;
            }
            _isProbeKickInProgress = true;
            try
            {
                await _heartbeatService.ProbeNowAsync(_address, cancellationToken);
                if (cancellationToken.IsCancellationRequested) return;
                DeviceHeartbeatSnapshot snapshot = _heartbeatService.GetSnapshot(_address);
                Dispatcher.UIThread.Post(() => ApplySnapshot(snapshot));
            }
            catch
            {
            }
            finally
            {
                _isProbeKickInProgress = false;
            }
        }

        private void OnHeartbeatStateChanged(HeartbeatStateChangedEventArgs args)
        {
            if (args == null || args.Snapshot == null)
            {
                return;
            }
            if (args.Snapshot.Address != _address)
            {
                return;
            }
            Dispatcher.UIThread.Post(() => ApplySnapshot(args.Snapshot));
        }

        private void OnLinkStatusChanged(DeviceLinkStatusChangedEventArgs args)
        {
            if (args == null || _heartbeatService == null)
            {
                return;
            }
            if (args.Address != _address)
            {
                return;
            }
            DeviceHeartbeatSnapshot snapshot = _heartbeatService.GetSnapshot(_address);
            Dispatcher.UIThread.Post(() => ApplySnapshot(snapshot));
        }

        private void ApplySnapshot(DeviceHeartbeatSnapshot snapshot)
        {
            _lastSnapshot = snapshot ?? new DeviceHeartbeatSnapshot { Address = _address };
            Boolean isEnabled = _lastSnapshot.Enabled;
            Int32 lastLatencyMs = _lastSnapshot.LastLatencyMs;
            Int32 failures = _lastSnapshot.ConsecutiveFailures;
            Boolean isProbing = _lastSnapshot.IsProbing;

            Boolean isConnected = false;
            if (_connectionOrchestratorRegistryService != null)
            {
                DeviceConnectionSnapshot link = _connectionOrchestratorRegistryService.GetSnapshot(_address);
                isConnected = link.State == DeviceLinkStateOptions.Connected;
            }

            Color healthy = Color.FromArgb(0xFF, 0x00, 0xB0, 0x4E);
            Color unstable = Color.FromArgb(0xFF, 0xE6, 0x7E, 0x22);
            Color danger = Color.FromArgb(0xFF, 0xD4, 0x2D, 0x2D);
            Color muted = Color.FromArgb(0x88, 0x80, 0x80, 0x80);

            Color baseColor = muted;
            Color riskColor = muted;

            if (!isEnabled)
            {
                baseColor = muted;
                riskColor = muted;
                TxtStatus.Text = _localizationControllerService != null ? _localizationControllerService.GetText(UiCatalogKeys.TitleHeartbeat) : UiCatalogKeys.TitleHeartbeat;
            }
            else
            {
                if (!isConnected)
                {
                    baseColor = unstable;
                    riskColor = unstable;
                    TxtStatus.Text = _localizationControllerService != null ? _localizationControllerService.GetText(UiCatalogKeys.TextDisconnected) : UiCatalogKeys.TextDisconnected;
                }
                else
                {
                    if (failures >= 3)
                    {
                        baseColor = danger;
                        riskColor = danger;
                        TxtStatus.Text = _localizationControllerService != null ? _localizationControllerService.GetText(UiCatalogKeys.TextDisconnected) : UiCatalogKeys.TextDisconnected;
                    }
                    else if (failures > 0)
                    {
                        baseColor = unstable;
                        riskColor = unstable;
                        TxtStatus.Text = _localizationControllerService != null ? _localizationControllerService.GetText(UiCatalogKeys.TextUnknown) : UiCatalogKeys.TextUnknown;
                    }
                    else
                    {
                        baseColor = healthy;
                        riskColor = healthy;
                        TxtStatus.Text = _localizationControllerService != null ? _localizationControllerService.GetText(UiCatalogKeys.TitleHeartbeat) : UiCatalogKeys.TitleHeartbeat;
                    }
                }
            }

            StatusDot.Background = new SolidColorBrush(baseColor);

            if (lastLatencyMs > 0 && isConnected)
            {
                String msSuffix = _localizationControllerService != null ? _localizationControllerService.GetText(UiCatalogKeys.LabelMillisecondsSuffix) : UiCatalogKeys.LabelMillisecondsSuffix;
                TxtLatencySmall.Text = lastLatencyMs.ToString() + " " + msSuffix;
                TxtLatencyBig.Text = lastLatencyMs.ToString() + " " + msSuffix;
            }
            else
            {
                TxtLatencySmall.Text = String.Empty;
                TxtLatencyBig.Text = " ";
            }

            if (_lastSnapshot.LastSuccessUtc != DateTime.MinValue && isConnected)
            {
                TxtLastSuccess.Text = _lastSnapshot.LastSuccessUtc.ToLocalTime().ToString("HH:mm:ss");
            }
            else
            {
                TxtLastSuccess.Text = String.Empty;
            }

            UpdateBadges(baseColor, riskColor, failures, isEnabled, isConnected);
            UpdateCountdownBadgeText();

            Double basePeriod = 3.20;
            Double baseAmplitude = (isEnabled && isConnected) ? 0.03 : 0.0;
            Heart.SetStyle(riskColor, basePeriod, baseAmplitude);

            Double pulsePeriod = ComputePulsePeriodSeconds(lastLatencyMs, failures);
            Double pulseAmplitude = (isProbing && isConnected) ? (failures >= 3 ? 0.06 : 0.12) : 0.0;
            Heart.SetPulse(isProbing && isConnected, pulsePeriod, pulseAmplitude);
        }

        private void OnCountdownTick(Object? sender, EventArgs e)
        {
            UpdateCountdownBadgeText();
        }

        private void UpdateCountdownBadgeText()
        {
            if (_heartbeatService == null)
            {
                return;
            }
            DateTime next = _lastSnapshot != null ? _lastSnapshot.NextPlannedProbeUtc : DateTime.MinValue;
            if (_lastSnapshot == null || !_lastSnapshot.Enabled || next == DateTime.MinValue)
            {
                TxtCountdownBadge.Text = String.Empty;
                return;
            }
            Boolean isConnected = false;
            if (_connectionOrchestratorRegistryService != null)
            {
                DeviceConnectionSnapshot link = _connectionOrchestratorRegistryService.GetSnapshot(_address);
                isConnected = link.State == DeviceLinkStateOptions.Connected;
            }
            if (!isConnected)
            {
                TxtCountdownBadge.Text = String.Empty;
                return;
            }
            TimeSpan until = next - DateTime.UtcNow;
            Int32 sec = (Int32)Math.Ceiling(until.TotalSeconds);
            if (sec <= 0)
            {
                TxtCountdownBadge.Text = "0";
                if (!_isProbeKickInProgress && !_lastSnapshot.IsProbing && _lastSnapshot.Enabled)
                {
                    _ = KickProbeNowAsync(_lifetimeCancellationTokenSource.Token);
                }
                return;
            }
            TxtCountdownBadge.Text = sec.ToString();
        }

        private static Double ComputePulsePeriodSeconds(Int32 latencyMs, Int32 failures)
        {
            Double basePeriod = 2.20;
            if (latencyMs > 0)
            {
                Double p = 1.80 + (latencyMs / 400.0);
                if (p < 1.60) p = 1.60;
                if (p > 3.50) p = 3.50;
                basePeriod = p;
            }
            if (failures > 0)
            {
                basePeriod = Math.Min(4.20, basePeriod * (1.10 + (0.08 * failures)));
            }
            return basePeriod;
        }

        private void UpdateBadges(Color baseColor, Color riskColor, Int32 failures, Boolean enabled, Boolean connected)
        {
            if (failures <= 0)
            {
                BadgeFailures.IsVisible = false;
            }
            else
            {
                BadgeFailures.IsVisible = true;
                BadgeFailures.Background = new SolidColorBrush(riskColor);
                TxtFailuresBadge.Text = failures.ToString();
                TxtFailuresBadge.Foreground = Brushes.White;
                TxtFailuresBadge.TextAlignment = TextAlignment.Center;
            }

            if (enabled && connected && _lastSnapshot != null && _lastSnapshot.NextPlannedProbeUtc != DateTime.MinValue)
            {
                BadgeCountdown.IsVisible = true;
                BadgeCountdown.Background = new SolidColorBrush(baseColor);
                TxtCountdownBadge.Foreground = Brushes.White;
                TxtCountdownBadge.TextAlignment = TextAlignment.Center;
            }
            else
            {
                BadgeCountdown.IsVisible = false;
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

            ContextMenu menu = new();

            MenuItem enableItem = new();
            String enableHeader = _localizationControllerService != null ? _localizationControllerService.GetText(UiCatalogKeys.MenuHeartbeatEnabled) : UiCatalogKeys.MenuHeartbeatEnabled;
            enableItem.Header = enableHeader;
            enableItem.ToggleType = MenuItemToggleType.CheckBox;
            enableItem.IsChecked = _lastSnapshot.Enabled;
            enableItem.Click += (Object? s, Avalonia.Interactivity.RoutedEventArgs a) =>
            {
                if (_heartbeatService == null)
                {
                    return;
                }
                _heartbeatService.SetEnabled(_address, !(_lastSnapshot.Enabled));
                DeviceHeartbeatSnapshot snap = _heartbeatService.GetSnapshot(_address);
                ApplySnapshot(snap);
            };

            MenuItem probeItem = new();
            String probeHeader = _localizationControllerService != null ? _localizationControllerService.GetText(UiCatalogKeys.MenuHeartbeatProbeNow) : UiCatalogKeys.MenuHeartbeatProbeNow;
            probeItem.Header = probeHeader;
            probeItem.Click += async (Object? s, Avalonia.Interactivity.RoutedEventArgs a) =>
            {
                try
                {
                    if (_heartbeatService == null)
                    {
                        return;
                    }
                    await _heartbeatService.ProbeNowAsync(_address, _lifetimeCancellationTokenSource.Token);
                    ApplySnapshot(_heartbeatService.GetSnapshot(_address));
                }
                catch
                {
                }
            };

            MenuItem periodMenu = new();
            String periodHeader = _localizationControllerService != null ? _localizationControllerService.GetText(UiCatalogKeys.LabelPeriod) : UiCatalogKeys.LabelPeriod;
            periodMenu.Header = periodHeader;

            Int32 currentPeriod = 10;
            if (_heartbeatService != null)
            {
                currentPeriod = _heartbeatService.GetPeriod(_address);
            }

            MenuItem p5 = BuildPeriodItem(UiCatalogKeys.Period5s, 5, currentPeriod);
            MenuItem p15 = BuildPeriodItem(UiCatalogKeys.Period15s, 15, currentPeriod);
            MenuItem p30 = BuildPeriodItem(UiCatalogKeys.Period30s, 30, currentPeriod);
            MenuItem p60 = BuildPeriodItem(UiCatalogKeys.Period1m, 60, currentPeriod);
            MenuItem p120 = BuildPeriodItem(UiCatalogKeys.Period2m, 120, currentPeriod);
            MenuItem p300 = BuildPeriodItem(UiCatalogKeys.Period5m, 300, currentPeriod);
            MenuItem p900 = BuildPeriodItem(UiCatalogKeys.Period15m, 900, currentPeriod);
            MenuItem p1800 = BuildPeriodItem(UiCatalogKeys.Period30m, 1800, currentPeriod);
            MenuItem p3600 = BuildPeriodItem(UiCatalogKeys.Period60m, 3600, currentPeriod);

            periodMenu.ItemsSource = new Object[] { p5, p15, p30, p60, p120, p300, p900, p1800, p3600 };

            menu.Items.Add(enableItem);
            menu.Items.Add(probeItem);
            menu.Items.Add(periodMenu);
            ContextMenuManager.Show(menu, this);
        }

        private MenuItem BuildPeriodItem(String captionKey, Int32 seconds, Int32 currentSeconds)
        {
            MenuItem item = new();
            String caption = _localizationControllerService != null ? _localizationControllerService.GetText(captionKey) : captionKey;
            item.Header = caption;
            item.ToggleType = MenuItemToggleType.Radio;
            item.IsChecked = currentSeconds == seconds;
            item.Click += (Object? s, Avalonia.Interactivity.RoutedEventArgs a) =>
            {
                if (_heartbeatService == null)
                {
                    return;
                }
                _heartbeatService.SetPeriod(_address, seconds);
                ApplySnapshot(_heartbeatService.GetSnapshot(_address));
            };
            return item;
        }
    }
}