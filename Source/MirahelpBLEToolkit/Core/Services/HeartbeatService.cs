using MirahelpBLEToolkit.Constants;
using MirahelpBLEToolkit.Core.Enums;
using MirahelpBLEToolkit.Core.Events;
using MirahelpBLEToolkit.Core.Interfaces;
using MirahelpBLEToolkit.Core.Models;
using MirahelpBLEToolkit.Core.Results;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace MirahelpBLEToolkit.Core.Services
{
    public sealed class HeartbeatService : IHeartbeatService
    {
        private sealed class Monitor
        {
            public UInt64 Address { get; set; }
            public Boolean IsEnabled { get; set; }
            public Boolean IsProbing { get; set; }
            public DateTime LastSuccessUtc { get; set; }
            public DateTime LastAttemptUtc { get; set; }
            public DateTime LastFailureUtc { get; set; }
            public Int32 LastLatencyMs { get; set; }
            public Int32 ConsecutiveFailures { get; set; }
            public String LastErrorCode { get; set; } = String.Empty;
            public DateTime NextPlannedProbeUtc { get; set; }
            public Int32 PeriodSeconds { get; set; }
            public CancellationTokenSource? CancellationTokenSource { get; set; }
            public Task? Loop { get; set; }
        }

        private readonly IEventBusService _eventBus;
        private readonly ConnectionService _connectionService;
        private readonly IDeviceRepositoryService _deviceRepository;
        private readonly IClockService _clock;

        private readonly ConcurrentDictionary<UInt64, Monitor> _monitorByAddress;
        private IDisposable? _linkSubscription;

        public HeartbeatService(IEventBusService eventBus, ConnectionService connectionService, IDeviceRepositoryService deviceRepository, IClockService clock)
        {
            _eventBus = eventBus;
            _connectionService = connectionService;
            _deviceRepository = deviceRepository;
            _clock = clock;
            _monitorByAddress = new ConcurrentDictionary<UInt64, Monitor>();
            _linkSubscription = null;
        }

        public void Start()
        {
            _linkSubscription?.Dispose();
            _linkSubscription = _eventBus.Subscribe<DeviceLinkStatusChangedEventArgs>(OnLinkStatusChanged);
        }

        public void Stop()
        {
            IDisposable? subscription = _linkSubscription;
            if (subscription != null)
            {
                try { subscription.Dispose(); } catch { }
            }
            _linkSubscription = null;

            foreach (Monitor monitor in _monitorByAddress.Values)
            {
                CancellationTokenSource? cancellation = monitor.CancellationTokenSource;
                if (cancellation != null)
                {
                    try { cancellation.Cancel(); } catch { }
                }
            }
        }

        public void SetEnabled(UInt64 address, Boolean isEnabled)
        {
            Monitor monitor = _monitorByAddress.GetOrAdd(address, (UInt64 a) => Create(a));
            Boolean wasEnabled = monitor.IsEnabled;
            monitor.IsEnabled = isEnabled;
            if (isEnabled && !wasEnabled)
            {
                monitor.NextPlannedProbeUtc = _clock.UtcNow.AddSeconds(Math.Max(1, monitor.PeriodSeconds));
                DeviceState? deviceState = _deviceRepository.TryGetByAddress(address);
                if (deviceState != null && deviceState.ConnectionStatus == ConnectionStatusOptions.Connected && !monitor.IsProbing)
                {
                    _ = Task.Run(async () =>
                    {
                        try { await ProbeNowAsync(address, CancellationToken.None); } catch { }
                    });
                }
            }
            Publish(monitor);
            EnsureLoop(monitor);
        }

        public Boolean IsEnabled(UInt64 address)
        {
            Monitor monitor;
            Boolean found = _monitorByAddress.TryGetValue(address, out monitor);
            if (!found || monitor == null)
            {
                return false;
            }
            return monitor.IsEnabled;
        }

        public void SetPeriod(UInt64 address, Int32 seconds)
        {
            Monitor monitor = _monitorByAddress.GetOrAdd(address, (UInt64 a) => Create(a));
            monitor.PeriodSeconds = Math.Max(1, seconds);
            monitor.NextPlannedProbeUtc = _clock.UtcNow.AddSeconds(monitor.PeriodSeconds);
            Publish(monitor);
            EnsureLoop(monitor);
        }

        public Int32 GetPeriod(UInt64 address)
        {
            Monitor monitor = _monitorByAddress.GetOrAdd(address, (UInt64 a) => Create(a));
            return Math.Max(1, monitor.PeriodSeconds);
        }

        public DeviceHeartbeatSnapshot GetSnapshot(UInt64 address)
        {
            Monitor monitor = _monitorByAddress.GetOrAdd(address, (UInt64 a) => Create(a));
            DeviceHeartbeatSnapshot snapshot = ToSnapshot(monitor);
            return snapshot;
        }

        public async Task<HeartbeatProbeResult> ProbeNowAsync(UInt64 address, CancellationToken cancellationToken)
        {
            Monitor monitor = _monitorByAddress.GetOrAdd(address, (UInt64 a) => Create(a));
            DateTime nowUtc = _clock.UtcNow;
            monitor.IsProbing = true;
            monitor.LastAttemptUtc = nowUtc;
            monitor.NextPlannedProbeUtc = nowUtc.AddSeconds(Math.Max(1, monitor.PeriodSeconds));
            Publish(monitor);
            HeartbeatProbeResult result = await DoProbeAsync(monitor, cancellationToken);
            Publish(monitor);
            return result;
        }

        private Monitor Create(UInt64 address)
        {
            Monitor monitor = new()
            {
                Address = address,
                IsEnabled = false,
                IsProbing = false,
                LastSuccessUtc = DateTime.MinValue,
                LastAttemptUtc = DateTime.MinValue,
                LastFailureUtc = DateTime.MinValue,
                LastLatencyMs = 0,
                ConsecutiveFailures = 0,
                LastErrorCode = String.Empty,
                NextPlannedProbeUtc = DateTime.MinValue,
                PeriodSeconds = 10,
                CancellationTokenSource = null,
                Loop = null
            };
            return monitor;
        }

        private void EnsureLoop(Monitor monitor)
        {
            if (monitor.Loop != null && !monitor.Loop.IsCompleted)
            {
                return;
            }
            CancellationTokenSource cancellationTokenSource = new();
            monitor.CancellationTokenSource = cancellationTokenSource;
            monitor.Loop = Task.Run(async () => await LoopAsync(monitor, cancellationTokenSource.Token), cancellationTokenSource.Token);
        }

        private async Task LoopAsync(Monitor monitor, CancellationToken cancellationToken)
        {
            try
            {
                using (PeriodicTimer timer = new(TimeSpan.FromSeconds(1)))
                {
                    while (await timer.WaitForNextTickAsync(cancellationToken))
                    {
                        if (!monitor.IsEnabled)
                        {
                            continue;
                        }
                        DeviceState? state = _deviceRepository.TryGetByAddress(monitor.Address);
                        if (state == null)
                        {
                            continue;
                        }
                        if (state.ConnectionStatus != ConnectionStatusOptions.Connected)
                        {
                            continue;
                        }
                        DateTime nowUtc = _clock.UtcNow;
                        if (monitor.IsProbing)
                        {
                            continue;
                        }
                        if (nowUtc < monitor.NextPlannedProbeUtc)
                        {
                            continue;
                        }
                        monitor.IsProbing = true;
                        monitor.LastAttemptUtc = nowUtc;
                        monitor.NextPlannedProbeUtc = nowUtc.AddSeconds(Math.Max(1, monitor.PeriodSeconds));
                        Publish(monitor);
                        _ = Task.Run(async () =>
                        {
                            await DoProbeAsync(monitor, cancellationToken);
                            Publish(monitor);
                        }, cancellationToken);
                    }
                }
            }
            catch
            {
            }
        }

        private async Task<HeartbeatProbeResult> DoProbeAsync(Monitor monitor, CancellationToken cancellationToken)
        {
            HeartbeatProbeResult result = new()
            {
                Address = monitor.Address,
                Succeeded = false,
                LatencyMs = 0,
                TimestampUtc = _clock.UtcNow,
                Error = String.Empty
            };
            try
            {
                CancellationTokenSource linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                linkedCancellation.CancelAfter(TimeSpan.FromSeconds(3));
                DateTime t0 = _clock.UtcNow;
                Boolean isSuccessful = await _connectionService.ProbeSessionAsync(monitor.Address, linkedCancellation.Token);
                DateTime t1 = _clock.UtcNow;
                Int32 elapsedMs = (Int32)Math.Max(0, Math.Round((t1 - t0).TotalMilliseconds));
                result.Succeeded = isSuccessful;
                result.LatencyMs = elapsedMs;
                result.TimestampUtc = t1;
                if (isSuccessful)
                {
                    monitor.LastSuccessUtc = t1;
                    monitor.LastLatencyMs = elapsedMs;
                    monitor.LastErrorCode = String.Empty;
                    monitor.ConsecutiveFailures = 0;
                }
                else
                {
                    monitor.LastFailureUtc = t1;
                    monitor.LastLatencyMs = 0;
                    monitor.LastErrorCode = AppStrings.ErrorCodeUnreachable;
                    monitor.ConsecutiveFailures = Math.Max(0, monitor.ConsecutiveFailures + 1);
                }
                return result;
            }
            catch (OperationCanceledException)
            {
                DateTime timestampUtc = _clock.UtcNow;
                result.Succeeded = false;
                result.LatencyMs = 0;
                result.TimestampUtc = timestampUtc;
                monitor.LastFailureUtc = timestampUtc;
                monitor.LastLatencyMs = 0;
                monitor.LastErrorCode = AppStrings.ErrorCodeTimeout;
                monitor.ConsecutiveFailures = Math.Max(0, monitor.ConsecutiveFailures + 1);
                return result;
            }
            catch (Exception exception)
            {
                DateTime timestampUtc = _clock.UtcNow;
                result.Succeeded = false;
                result.LatencyMs = 0;
                result.TimestampUtc = timestampUtc;
                monitor.LastFailureUtc = timestampUtc;
                monitor.LastLatencyMs = 0;
                monitor.LastErrorCode = exception.Message ?? String.Empty;
                monitor.ConsecutiveFailures = Math.Max(0, monitor.ConsecutiveFailures + 1);
                return result;
            }
            finally
            {
                monitor.IsProbing = false;
            }
        }

        private static DeviceHeartbeatSnapshot ToSnapshot(Monitor monitor)
        {
            DeviceHeartbeatSnapshot snapshot = new()
            {
                Address = monitor.Address,
                Enabled = monitor.IsEnabled,
                IsProbing = monitor.IsProbing,
                LastSuccessUtc = monitor.LastSuccessUtc,
                LastAttemptUtc = monitor.LastAttemptUtc,
                LastFailureUtc = monitor.LastFailureUtc,
                LastLatencyMs = monitor.LastLatencyMs,
                ConsecutiveFailures = monitor.ConsecutiveFailures,
                LastError = monitor.LastErrorCode ?? String.Empty,
                NextPlannedProbeUtc = monitor.NextPlannedProbeUtc
            };
            return snapshot;
        }

        private void Publish(Monitor monitor)
        {
            DeviceHeartbeatSnapshot snapshot = ToSnapshot(monitor);
            HeartbeatStateChangedEventArgs changedEvent = new() { Snapshot = snapshot };
            _eventBus.Publish(changedEvent);
        }

        private void OnLinkStatusChanged(DeviceLinkStatusChangedEventArgs changeEvent)
        {
            if (changeEvent == null)
            {
                return;
            }
            if (changeEvent.Status != ConnectionStatusOptions.Connected)
            {
                return;
            }
            Monitor monitor = _monitorByAddress.GetOrAdd(changeEvent.Address, (UInt64 a) => Create(a));
            if (!monitor.IsEnabled)
            {
                return;
            }
            if (monitor.IsProbing)
            {
                return;
            }
            monitor.NextPlannedProbeUtc = _clock.UtcNow;
            Publish(monitor);
            _ = Task.Run(async () =>
            {
                try { await ProbeNowAsync(changeEvent.Address, CancellationToken.None); } catch { }
            });
        }
    }
}