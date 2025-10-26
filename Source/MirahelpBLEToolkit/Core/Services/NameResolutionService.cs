using MirahelpBLEToolkit.Configuration;
using MirahelpBLEToolkit.Constants;
using MirahelpBLEToolkit.Core.Controllers;
using MirahelpBLEToolkit.Core.Enums;
using MirahelpBLEToolkit.Core.Interfaces;
using MirahelpBLEToolkit.Core.Models;
using MirahelpBLEToolkit.Core.Results;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace MirahelpBLEToolkit.Core.Services
{
    public sealed class NameResolutionService : INameResolutionService
    {
        private sealed class RetryInfo
        {
            public Int32 AttemptIndex { get; set; }
            public TimeSpan CurrentDelay { get; set; }
            public TimeSpan CurrentTimeout { get; set; }
            public DateTime NextPlannedUtc { get; set; }
        }

        private static readonly TimeSpan RetryInitialDelay = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan RetryMaxDelay = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan TimeoutInitial = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan TimeoutMax = TimeSpan.FromSeconds(10);

        private readonly IBleProviderService _provider;
        private readonly IDeviceRepositoryService _deviceRepository;
        private readonly IMessageRepository _messageRepository;
        private readonly IClockService _clock;

        private readonly ConcurrentQueue<UInt64> _pendingQueue;
        private readonly ConcurrentDictionary<UInt64, Byte> _pendingSet;

        private DeviceWatcher? _deviceWatcher;
        private CancellationTokenSource? _heartbeatCancellationTokenSource;
        private Task? _heartbeatTask;

        private readonly SemaphoreSlim _resolveConcurrencyGate;
        private readonly SemaphoreSlim _gapResolveGate;

        private readonly ConcurrentDictionary<UInt64, RetryInfo> _retryByAddress;

        public NameResolutionService(IBleProviderService provider, IDeviceRepositoryService deviceRepository, IMessageRepository messageRepository, IClockService clock)
        {
            _provider = provider;
            _deviceRepository = deviceRepository;
            _messageRepository = messageRepository;
            _clock = clock;
            _pendingQueue = new ConcurrentQueue<UInt64>();
            _pendingSet = new ConcurrentDictionary<UInt64, Byte>();
            Int32 maxConcurrency = Math.Max(1, AppConfig.NameResolveMaxConcurrency);
            _resolveConcurrencyGate = new SemaphoreSlim(maxConcurrency, maxConcurrency);
            _gapResolveGate = new SemaphoreSlim(1, 1);
            _retryByAddress = new ConcurrentDictionary<UInt64, RetryInfo>();
        }

        public void Start()
        {
            if (_heartbeatTask != null)
            {
                return;
            }

            String deviceSelectorAqs = BluetoothLEDevice.GetDeviceSelector();
            _deviceWatcher = DeviceInformation.CreateWatcher(deviceSelectorAqs);
            _deviceWatcher.Added += OnDeviceInfoAdded;
            _deviceWatcher.Updated += OnDeviceInfoUpdated;
            _deviceWatcher.Start();

            _heartbeatCancellationTokenSource = new CancellationTokenSource();
            _heartbeatTask = Task.Run(() => HeartbeatAsync(_heartbeatCancellationTokenSource.Token), _heartbeatCancellationTokenSource.Token);
        }

        public void Stop()
        {
            if (_deviceWatcher != null)
            {
                try
                {
                    _deviceWatcher.Added -= OnDeviceInfoAdded;
                    _deviceWatcher.Updated -= OnDeviceInfoUpdated;
                    _deviceWatcher.Stop();
                }
                catch
                {
                }
                _deviceWatcher = null;
            }

            CancellationTokenSource? source = _heartbeatCancellationTokenSource;
            if (source != null)
            {
                try { source.Cancel(); } catch { }
            }
            _heartbeatCancellationTokenSource = null;
            _heartbeatTask = null;
        }

        public void Enqueue(UInt64 address)
        {
            if (address == 0)
            {
                return;
            }
            ResetRetryOnEnqueue(address);
            Boolean wasAdded = _pendingSet.TryAdd(address, 0);
            if (!wasAdded)
            {
                return;
            }
            _pendingQueue.Enqueue(address);
            _ = Task.Run(async () =>
            {
                try
                {
                    await RunResolveAsync(address, CancellationToken.None);
                }
                catch
                {
                }
            });
        }

        public async Task FetchNowAsync(UInt64 address, CancellationToken cancellationToken)
        {
            await _resolveConcurrencyGate.WaitAsync(cancellationToken);
            try
            {
                await TryResolveFullAsync(address, cancellationToken);
            }
            finally
            {
                try { _resolveConcurrencyGate.Release(); } catch { }
            }
        }

        private void OnDeviceInfoAdded(DeviceWatcher sender, DeviceInformation deviceInformation)
        {
            _ = Task.Run(async () => { await TryUpdateFromIdAsync(deviceInformation.Id); });
        }

        private void OnDeviceInfoUpdated(DeviceWatcher sender, DeviceInformationUpdate deviceInformationUpdate)
        {
            _ = Task.Run(async () => { await TryUpdateFromIdAsync(deviceInformationUpdate.Id); });
        }

        private async Task TryUpdateFromIdAsync(String deviceId)
        {
            BluetoothLEDevice? device = null;
            try
            {
                device = await BluetoothLEDevice.FromIdAsync(deviceId);
                if (device == null)
                {
                    return;
                }
                UInt64 address = device.BluetoothAddress;
                if (address == 0)
                {
                    return;
                }
                String osName = device.Name ?? String.Empty;
                UpdateName(address, osName);
            }
            catch
            {
            }
            finally
            {
                if (device != null)
                {
                    try { device.Dispose(); } catch { }
                }
            }
        }

        private void UpdateName(UInt64 address, String candidateName)
        {
            if (String.IsNullOrWhiteSpace(candidateName))
            {
                return;
            }
            if (NameSelectionController.IsAddressLike(candidateName, address))
            {
                return;
            }

            DeviceState? state = _deviceRepository.TryGetByAddress(address);
            if (state == null)
            {
                DeviceState newState = new()
                {
                    DeviceId = address.ToString(AppStrings.FormatHexUpper),
                    Address = address,
                    Name = candidateName,
                    IsPaired = false,
                    FirstSeenUtc = _clock.UtcNow,
                    LastSeenUtc = _clock.UtcNow,
                    Pinned = false,
                    ConnectionStatus = ConnectionStatusOptions.Disconnected
                };
                _deviceRepository.Upsert(newState);
            }
            else
            {
                if (String.IsNullOrWhiteSpace(state.Name) || NameSelectionController.IsAddressLike(state.Name, state.Address))
                {
                    state.Name = candidateName;
                    state.LastSeenUtc = _clock.UtcNow;
                    _deviceRepository.Upsert(state);
                }
            }

            RetryInfo removed;
            _retryByAddress.TryRemove(address, out removed);
        }

        private async Task HeartbeatAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await DrainPendingAsync(cancellationToken);
                    await DrainDueRetriesAsync(cancellationToken);
                    try { await Task.Delay(AppConfig.NameResolveHeartbeatInterval, cancellationToken); } catch { }
                }
            }
            catch
            {
            }
        }

        private async Task DrainPendingAsync(CancellationToken cancellationToken)
        {
            List<UInt64> batch = new();
            Int32 maxBatchSize = 64;
            while (batch.Count < maxBatchSize && _pendingQueue.TryDequeue(out UInt64 address))
            {
                Byte removed;
                _pendingSet.TryRemove(address, out removed);
                batch.Add(address);
            }
            if (batch.Count == 0)
            {
                return;
            }

            List<Task> tasks = new();
            foreach (UInt64 pendingAddress in batch)
            {
                tasks.Add(RunResolveAsync(pendingAddress, cancellationToken));
            }
            try { await Task.WhenAll(tasks); } catch { }
        }

        private async Task DrainDueRetriesAsync(CancellationToken cancellationToken)
        {
            DateTime nowUtc = _clock.UtcNow;
            UInt64[] keys = System.Linq.Enumerable.ToArray(_retryByAddress.Keys);
            foreach (UInt64 address in keys)
            {
                RetryInfo info;
                Boolean found = _retryByAddress.TryGetValue(address, out info);
                if (!found || info == null)
                {
                    continue;
                }
                if (info.NextPlannedUtc <= nowUtc)
                {
                    Boolean added = _pendingSet.TryAdd(address, 0);
                    if (added)
                    {
                        _pendingQueue.Enqueue(address);
                    }
                }
            }
        }

        private async Task RunResolveAsync(UInt64 address, CancellationToken cancellationToken)
        {
            await _resolveConcurrencyGate.WaitAsync(cancellationToken);
            try
            {
                await TryResolveFullAsync(address, cancellationToken);
            }
            finally
            {
                try { _resolveConcurrencyGate.Release(); } catch { }
            }
        }

        private async Task TryResolveFullAsync(UInt64 address, CancellationToken cancellationToken)
        {
            RetryInfo info = GetOrCreateRetryInfo(address);
            TimeSpan resolveTimeout = info != null ? info.CurrentTimeout : TimeoutInitial;

            using (CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                linked.CancelAfter(resolveTimeout);
                Boolean hasAny = await TryResolveOsNameForAddressAsync(address, linked.Token);
                if (hasAny)
                {
                    RetryInfo removed;
                    _retryByAddress.TryRemove(address, out removed);
                    return;
                }
            }

            using (CancellationTokenSource shortGapCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                shortGapCts.CancelAfter(resolveTimeout);
                await _gapResolveGate.WaitAsync(shortGapCts.Token);
                try
                {
                    Boolean hasGap = await TryResolveGapNameForAddressAsync(address, shortGapCts.Token);
                    if (hasGap)
                    {
                        RetryInfo removed;
                        _retryByAddress.TryRemove(address, out removed);
                        return;
                    }
                }
                finally
                {
                    try { _gapResolveGate.Release(); } catch { }
                }
            }

            MarkFailure(address);
        }

        private async Task<Boolean> TryResolveOsNameForAddressAsync(UInt64 address, CancellationToken cancellationToken)
        {
            IBleDeviceService? device = null;
            try
            {
                device = await _provider.FromAddressAsync(address, cancellationToken);
                if (device == null)
                {
                    return false;
                }
                String osName = device.Name ?? String.Empty;
                if (!String.IsNullOrWhiteSpace(osName) && !NameSelectionController.IsAddressLike(osName, address))
                {
                    UpdateName(address, osName);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (device != null)
                {
                    try { device.Dispose(); } catch { }
                }
            }
        }

        private async Task<Boolean> TryResolveGapNameForAddressAsync(UInt64 address, CancellationToken cancellationToken)
        {
            IBleDeviceService? device = null;
            IGattServiceService? gapService = null;
            IGattCharacteristicService? gapNameCharacteristic = null;
            try
            {
                device = await _provider.FromAddressAsync(address, cancellationToken);
                if (device == null)
                {
                    return false;
                }
                Guid gapServiceUuid = new("00001800-0000-1000-8000-00805F9B34FB");
                Guid gapNameUuid = new("00002A00-0000-1000-8000-00805F9B34FB");

                GattServicesResult servicesForUuid = await device.GetServicesForUuidAsync(gapServiceUuid, cancellationToken);
                if (servicesForUuid.Services.Count == 0)
                {
                    return false;
                }
                gapService = servicesForUuid.Services[0];

                GattCharacteristicsResult characteristicsForUuid = await gapService.GetCharacteristicsForUuidAsync(gapNameUuid, cancellationToken);
                if (characteristicsForUuid.Characteristics.Count == 0)
                {
                    return false;
                }
                gapNameCharacteristic = characteristicsForUuid.Characteristics[0];

                GattReadResult readResult = await gapNameCharacteristic.ReadAsync(CacheModeOptions.Uncached, cancellationToken);
                if (readResult.Status != GattCommunicationStatusOptions.Success || readResult.Data == null || readResult.Data.Length == 0)
                {
                    return false;
                }
                String name = Encoding.UTF8.GetString(readResult.Data);
                if (String.IsNullOrWhiteSpace(name))
                {
                    return false;
                }
                if (NameSelectionController.IsAddressLike(name, address))
                {
                    return false;
                }
                UpdateName(address, name);
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (gapNameCharacteristic != null)
                {
                    try { gapNameCharacteristic.Dispose(); } catch { }
                }
                if (gapService != null)
                {
                    try { gapService.Dispose(); } catch { }
                }
                if (device != null)
                {
                    try { device.Dispose(); } catch { }
                }
            }
        }

        private RetryInfo GetOrCreateRetryInfo(UInt64 address)
        {
            return _retryByAddress.GetOrAdd(address, (UInt64 _) => new RetryInfo
            {
                AttemptIndex = 0,
                CurrentDelay = RetryInitialDelay,
                CurrentTimeout = TimeoutInitial,
                NextPlannedUtc = _clock.UtcNow
            });
        }

        private void ResetRetryOnEnqueue(UInt64 address)
        {
            RetryInfo info = new()
            {
                AttemptIndex = 0,
                CurrentDelay = RetryInitialDelay,
                CurrentTimeout = TimeoutInitial,
                NextPlannedUtc = _clock.UtcNow
            };
            _retryByAddress[address] = info;
        }

        private void MarkFailure(UInt64 address)
        {
            RetryInfo info = GetOrCreateRetryInfo(address);
            Int32 nextAttempt = info.AttemptIndex + 1;
            Double nextDelayMs = Math.Min(RetryMaxDelay.TotalMilliseconds, info.CurrentDelay.TotalMilliseconds * 2.0);
            Double nextTimeoutMs = Math.Min(TimeoutMax.TotalMilliseconds, info.CurrentTimeout.TotalMilliseconds * 1.5);
            info.AttemptIndex = nextAttempt;
            info.CurrentDelay = TimeSpan.FromMilliseconds(nextDelayMs);
            info.CurrentTimeout = TimeSpan.FromMilliseconds(nextTimeoutMs);
            Int32 jitterPermil = RandomNumberGenerator.GetInt32(850, 1201);
            Double jitterFactor = jitterPermil / 1000.0;
            TimeSpan jitteredDelay = TimeSpan.FromMilliseconds(Math.Max(1.0, info.CurrentDelay.TotalMilliseconds * jitterFactor));
            info.NextPlannedUtc = _clock.UtcNow.Add(jitteredDelay);
            _retryByAddress[address] = info;
        }
    }
}