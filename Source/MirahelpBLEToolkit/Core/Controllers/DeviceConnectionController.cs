using MirahelpBLEToolkit.Constants;
using MirahelpBLEToolkit.Core.Enums;
using MirahelpBLEToolkit.Core.Events;
using MirahelpBLEToolkit.Core.Interfaces;
using MirahelpBLEToolkit.Core.Models;
using MirahelpBLEToolkit.Core.Services;
using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace MirahelpBLEToolkit.Core.Controllers
{
    public sealed class DeviceConnectionController : IDeviceConnectionControllerService
    {
        private readonly UInt64 _address;
        private readonly ConnectionService _connectionService;
        private readonly IDeviceRepositoryService _deviceRepository;
        private readonly IEventBusService _eventBus;
        private readonly IClockService _clock;

        private readonly Object _synchronizationObject = new();

        private DeviceLinkStateOptions _state;
        private Guid _attemptIdentifier;
        private Int64 _sequenceNumber;
        private Int32 _busyDepth;
        private Boolean _isAutoReconnectEnabled;
        private DateTime _connectedSinceUtc;
        private DateTime _nextReconnectUtc;
        private String _lastErrorCode;

        private CancellationTokenSource? _currentAttemptCancellation;
        private Boolean _isReconnectWorkerActive;
        private Task? _reconnectWorker;

        private readonly IDisposable _linkSubscription;

        public DeviceConnectionController(UInt64 address, ConnectionService connectionService, IDeviceRepositoryService deviceRepository, IEventBusService eventBus, IClockService clock)
        {
            _address = address;
            _connectionService = connectionService;
            _deviceRepository = deviceRepository;
            _eventBus = eventBus;
            _clock = clock;

            _state = DeviceLinkStateOptions.Disconnected;
            _attemptIdentifier = Guid.Empty;
            _sequenceNumber = 0;
            _busyDepth = 0;
            _isAutoReconnectEnabled = false;
            _connectedSinceUtc = DateTime.MinValue;
            _nextReconnectUtc = DateTime.MinValue;
            _lastErrorCode = String.Empty;

            _currentAttemptCancellation = null;
            _isReconnectWorkerActive = false;
            _reconnectWorker = null;

            _linkSubscription = _eventBus.Subscribe<DeviceLinkStatusChangedEventArgs>(OnLinkStatusChanged);
        }

        public DeviceConnectionSnapshot GetSnapshot()
        {
            lock (_synchronizationObject)
            {
                DeviceConnectionSnapshot snapshot = new()
                {
                    Address = _address,
                    State = _state,
                    AttemptId = _attemptIdentifier,
                    Sequence = _sequenceNumber,
                    BusyDepth = _busyDepth,
                    AutoReconnectEnabled = _isAutoReconnectEnabled,
                    ConnectedSinceUtc = _connectedSinceUtc,
                    NextReconnectUtc = _nextReconnectUtc,
                    LastError = _lastErrorCode ?? String.Empty
                };
                return snapshot;
            }
        }

        public Guid RequestConnect()
        {
            lock (_synchronizationObject)
            {
                if (_state == DeviceLinkStateOptions.Connected)
                {
                    return _attemptIdentifier;
                }
                if (_state == DeviceLinkStateOptions.Connecting)
                {
                    return _attemptIdentifier;
                }
                _attemptIdentifier = Guid.NewGuid();
                _sequenceNumber++;
                _state = DeviceLinkStateOptions.Connecting;
                _busyDepth++;
                _lastErrorCode = String.Empty;

                CancellationTokenSource? previousAttemptCancellation = _currentAttemptCancellation;
                if (previousAttemptCancellation != null)
                {
                    try { previousAttemptCancellation.Cancel(); } catch { }
                    try { previousAttemptCancellation.Dispose(); } catch { }
                }
                _currentAttemptCancellation = new CancellationTokenSource();
                Publish();
                CancellationToken cancellationToken = _currentAttemptCancellation.Token;
                Guid capturedAttemptIdentifier = _attemptIdentifier;
                _ = Task.Run(async () => await ConnectAsync(capturedAttemptIdentifier, cancellationToken));
                return capturedAttemptIdentifier;
            }
        }

        public void RequestDisconnect(Boolean isManual)
        {
            CancellationTokenSource? cancellationToSignal = null;
            lock (_synchronizationObject)
            {
                if (_state == DeviceLinkStateOptions.Disconnecting)
                {
                    return;
                }
                _sequenceNumber++;
                _state = DeviceLinkStateOptions.Disconnecting;
                _busyDepth++;
                Publish();
                cancellationToSignal = _currentAttemptCancellation;
            }
            if (cancellationToSignal != null)
            {
                try { cancellationToSignal.Cancel(); } catch { }
            }
            try
            {
                _connectionService.Disconnect(_address);
            }
            catch
            {
            }
            lock (_synchronizationObject)
            {
                _sequenceNumber++;
                _state = DeviceLinkStateOptions.Disconnected;
                _connectedSinceUtc = DateTime.MinValue;
                if (isManual)
                {
                    Int32 seconds = RandomNumberGenerator.GetInt32(8, 13);
                    _nextReconnectUtc = _clock.UtcNow.AddSeconds(seconds);
                }
                if (_busyDepth > 0) _busyDepth--;
                Publish();
            }
            EnsureReconnectWorker();
        }

        public void SetAutoReconnectEnabled(Boolean isEnabled)
        {
            lock (_synchronizationObject)
            {
                _isAutoReconnectEnabled = isEnabled;
                if (_isAutoReconnectEnabled && _state != DeviceLinkStateOptions.Connected)
                {
                    _nextReconnectUtc = _clock.UtcNow;
                }
                Publish();
            }
            EnsureReconnectWorker();
        }

        private async Task ConnectAsync(Guid attemptIdentifier, CancellationToken cancellationToken)
        {
            Boolean isSuccessful = false;
            String errorCode = String.Empty;
            try
            {
                isSuccessful = await _connectionService.EnsureConnectedAsync(_address, CacheModeOptions.Uncached, cancellationToken);
                if (!isSuccessful && String.IsNullOrWhiteSpace(errorCode))
                {
                    errorCode = AppStrings.ErrorCodeUnreachable;
                }
            }
            catch (TimeoutException)
            {
                isSuccessful = false;
                errorCode = AppStrings.ErrorCodeTimeout;
            }
            catch (OperationCanceledException)
            {
                isSuccessful = false;
                errorCode = AppStrings.ErrorCodeCanceled;
            }
            catch (Exception exception)
            {
                isSuccessful = false;
                errorCode = exception.Message ?? String.Empty;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                lock (_synchronizationObject)
                {
                    if (_busyDepth > 0) _busyDepth--;
                    Publish();
                }
                return;
            }

            if (isSuccessful)
            {
                lock (_synchronizationObject)
                {
                    if (_attemptIdentifier != attemptIdentifier)
                    {
                        if (_busyDepth > 0) _busyDepth--;
                        Publish();
                        return;
                    }
                    _sequenceNumber++;
                    _state = DeviceLinkStateOptions.Connected;
                    _connectedSinceUtc = _clock.UtcNow;
                    _lastErrorCode = String.Empty;
                    if (_busyDepth > 0) _busyDepth--;
                    Publish();
                }
            }
            else
            {
                lock (_synchronizationObject)
                {
                    if (_attemptIdentifier != attemptIdentifier)
                    {
                        if (_busyDepth > 0) _busyDepth--;
                        Publish();
                        return;
                    }
                    _sequenceNumber++;
                    _state = DeviceLinkStateOptions.Failed;
                    _lastErrorCode = errorCode ?? String.Empty;
                    Int32 delaySeconds = _nextReconnectUtc > _clock.UtcNow ? (Int32)Math.Max(1, (_nextReconnectUtc - _clock.UtcNow).TotalSeconds) : 0;
                    if (delaySeconds <= 0)
                    {
                        Int32 jitterSeconds = RandomNumberGenerator.GetInt32(1, 4);
                        _nextReconnectUtc = _clock.UtcNow.AddSeconds(jitterSeconds);
                    }
                    if (_busyDepth > 0) _busyDepth--;
                    Publish();
                }
                EnsureReconnectWorker();
            }
        }

        private void EnsureReconnectWorker()
        {
            Boolean isStartRequired = false;
            lock (_synchronizationObject)
            {
                if (_isAutoReconnectEnabled && !_isReconnectWorkerActive)
                {
                    isStartRequired = true;
                    _isReconnectWorkerActive = true;
                }
            }
            if (!isStartRequired)
            {
                return;
            }
            _reconnectWorker = Task.Run(async () =>
            {
                while (true)
                {
                    Boolean isEnabled;
                    DeviceLinkStateOptions currentState;
                    DateTime nextPlannedReconnectUtc;
                    lock (_synchronizationObject)
                    {
                        isEnabled = _isAutoReconnectEnabled;
                        currentState = _state;
                        nextPlannedReconnectUtc = _nextReconnectUtc;
                    }
                    if (!isEnabled)
                    {
                        break;
                    }
                    if (currentState == DeviceLinkStateOptions.Connected)
                    {
                        break;
                    }
                    DateTime nowUtc = _clock.UtcNow;
                    TimeSpan waitDuration = nextPlannedReconnectUtc > nowUtc ? nextPlannedReconnectUtc - nowUtc : TimeSpan.Zero;
                    if (waitDuration > TimeSpan.Zero)
                    {
                        try { await Task.Delay(waitDuration); } catch { }
                    }
                    Guid attemptIdentifier = RequestConnect();
                    await Task.Yield();
                    Int32 iterationCount = 0;
                    while (true)
                    {
                        DeviceLinkStateOptions stateSnapshot;
                        lock (_synchronizationObject)
                        {
                            stateSnapshot = _state;
                        }
                        if (stateSnapshot != DeviceLinkStateOptions.Connecting)
                        {
                            break;
                        }
                        try { await Task.Delay(100); } catch { }
                        iterationCount++;
                        if (iterationCount > 300)
                        {
                            break;
                        }
                    }
                    DeviceLinkStateOptions finalState;
                    lock (_synchronizationObject)
                    {
                        finalState = _state;
                        if (finalState != DeviceLinkStateOptions.Connected)
                        {
                            Double remaining = _nextReconnectUtc > _clock.UtcNow ? (_nextReconnectUtc - _clock.UtcNow).TotalSeconds : 1.0;
                            Int32 baseNextSeconds = (Int32)Math.Min(30, Math.Max(1, Math.Round(remaining * 2)));
                            Int32 jitterPermil = RandomNumberGenerator.GetInt32(800, 1201);
                            Double jitterFactor = jitterPermil / 1000.0;
                            Int32 jitteredNextSeconds = (Int32)Math.Max(1, Math.Min(30, Math.Round(baseNextSeconds * jitterFactor)));
                            _nextReconnectUtc = _clock.UtcNow.AddSeconds(jitteredNextSeconds);
                        }
                        else
                        {
                            Int32 jitterSeconds = RandomNumberGenerator.GetInt32(1, 3);
                            _nextReconnectUtc = _clock.UtcNow.AddSeconds(jitterSeconds);
                        }
                    }
                }
                lock (_synchronizationObject)
                {
                    _isReconnectWorkerActive = false;
                }
            });
        }

        private void Publish()
        {
            DeviceConnectionSnapshot snapshot = new()
            {
                Address = _address,
                State = _state,
                AttemptId = _attemptIdentifier,
                Sequence = _sequenceNumber,
                BusyDepth = _busyDepth,
                AutoReconnectEnabled = _isAutoReconnectEnabled,
                ConnectedSinceUtc = _connectedSinceUtc,
                NextReconnectUtc = _nextReconnectUtc,
                LastError = _lastErrorCode ?? String.Empty
            };
            DeviceConnectionStateChangedEventArgs changedEvent = new() { Snapshot = snapshot };
            _eventBus.Publish(changedEvent);
        }

        private void OnLinkStatusChanged(DeviceLinkStatusChangedEventArgs changeEvent)
        {
            if (changeEvent == null || changeEvent.Address != _address)
            {
                return;
            }
            if (changeEvent.Status == ConnectionStatusOptions.Connected)
            {
                lock (_synchronizationObject)
                {
                    _sequenceNumber++;
                    _state = DeviceLinkStateOptions.Connected;
                    if (_connectedSinceUtc == DateTime.MinValue)
                    {
                        _connectedSinceUtc = _clock.UtcNow;
                    }
                    Publish();
                }
            }
            else
            {
                lock (_synchronizationObject)
                {
                    _sequenceNumber++;
                    _state = DeviceLinkStateOptions.Disconnected;
                    _connectedSinceUtc = DateTime.MinValue;
                    if (_isAutoReconnectEnabled)
                    {
                        Int32 jitterSeconds = RandomNumberGenerator.GetInt32(1, 3);
                        _nextReconnectUtc = _clock.UtcNow.AddSeconds(jitterSeconds);
                    }
                    Publish();
                }
                EnsureReconnectWorker();
            }
        }
    }
}