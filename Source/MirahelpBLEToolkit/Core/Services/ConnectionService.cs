using MirahelpBLEToolkit.Configuration;
using MirahelpBLEToolkit.Constants;
using MirahelpBLEToolkit.Core.Enums;
using MirahelpBLEToolkit.Core.Events;
using MirahelpBLEToolkit.Core.Interfaces;
using MirahelpBLEToolkit.Core.Models;
using MirahelpBLEToolkit.Core.Results;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace MirahelpBLEToolkit.Core.Services
{
    public sealed class ConnectionService : IConnectionService
    {
        private readonly IBleProviderService _provider;
        private readonly IDeviceRepositoryService _deviceRepository;
        private readonly IEventBusService _eventBus;
        private readonly IMessageRepository _messageRepository;

        private readonly ConcurrentDictionary<UInt64, SemaphoreSlim> _connectGateByAddress = new();
        private readonly ConcurrentDictionary<UInt64, IBleDeviceService> _trackedDeviceByAddress = new();
        private readonly ConcurrentDictionary<UInt64, Action<ConnectionStatusOptions>> _statusHandlerByAddress = new();

        public ConnectionService(IBleProviderService provider, IDeviceRepositoryService deviceRepository, IEventBusService eventBus, IMessageRepository messageRepository)
        {
            _provider = provider;
            _deviceRepository = deviceRepository;
            _eventBus = eventBus;
            _messageRepository = messageRepository;
        }

        public async Task<Boolean> EnsureConnectedAsync(UInt64 address, CacheModeOptions cacheMode, CancellationToken cancellationToken)
        {
            IBleDeviceService? alreadyTrackedDevice;
            Boolean isTracked = _trackedDeviceByAddress.TryGetValue(address, out alreadyTrackedDevice);
            if (isTracked && alreadyTrackedDevice != null && alreadyTrackedDevice.ConnectionStatus == ConnectionStatusOptions.Connected)
            {
                UpdateRepositoryStatus(address, ConnectionStatusOptions.Connected);
                return true;
            }

            SemaphoreSlim connectionGate = _connectGateByAddress.GetOrAdd(address, (UInt64 a) => new SemaphoreSlim(1, 1));

            using (CancellationTokenSource linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                linkedCancellation.CancelAfter(AppConfig.ConnectAttemptTimeout);

                await connectionGate.WaitAsync(linkedCancellation.Token);
                try
                {
                    IBleDeviceService? candidateDevice = await _provider.FromAddressAsync(address, linkedCancellation.Token);
                    if (candidateDevice == null)
                    {
                        UpdateRepositoryStatus(address, ConnectionStatusOptions.Disconnected);
                        return false;
                    }

                    List<CacheModeOptions> cacheModes = new();
                    cacheModes.Add(CacheModeOptions.Uncached);
                    if (cacheMode != CacheModeOptions.Uncached) cacheModes.Add(cacheMode);
                    cacheModes.Add(CacheModeOptions.Cached);

                    TimeSpan currentRetryDelay = AppConfig.ConnectRetryInitialDelay;
                    Boolean isSuccessful = false;

                    while (!linkedCancellation.IsCancellationRequested)
                    {
                        foreach (CacheModeOptions mode in cacheModes)
                        {
                            if (linkedCancellation.IsCancellationRequested)
                            {
                                break;
                            }
                            GattServicesResult services = await candidateDevice.GetServicesAsync(mode, linkedCancellation.Token);
                            if (services.Status == GattCommunicationStatusOptions.Success)
                            {
                                isSuccessful = true;
                                break;
                            }
                        }
                        if (isSuccessful)
                        {
                            break;
                        }
                        if (linkedCancellation.IsCancellationRequested)
                        {
                            break;
                        }
                        TimeSpan boundedDelay = currentRetryDelay > AppConfig.ConnectRetryMaxDelay ? AppConfig.ConnectRetryMaxDelay : currentRetryDelay;
                        Int32 jitterPermil = RandomNumberGenerator.GetInt32(800, 1201);
                        Double jitterFactor = jitterPermil / 1000.0;
                        TimeSpan jitteredDelay = TimeSpan.FromMilliseconds(Math.Max(1.0, boundedDelay.TotalMilliseconds * jitterFactor));
                        try { await Task.Delay(jitteredDelay, linkedCancellation.Token); } catch { }
                        Double nextDelayMilliseconds = Math.Min(AppConfig.ConnectRetryMaxDelay.TotalMilliseconds, boundedDelay.TotalMilliseconds * 2.0);
                        currentRetryDelay = TimeSpan.FromMilliseconds(nextDelayMilliseconds);
                    }

                    if (!isSuccessful && linkedCancellation.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                    {
                        try { candidateDevice.Dispose(); } catch { }
                        throw new TimeoutException(AppStrings.ErrorCodeTimeout);
                    }

                    if (!isSuccessful)
                    {
                        try { candidateDevice.Dispose(); } catch { }
                        UpdateRepositoryStatus(address, ConnectionStatusOptions.Disconnected);
                        return false;
                    }

                    try
                    {
                        GattSessionInfo sessionInfo = await candidateDevice.GetGattSessionInfoAsync(linkedCancellation.Token);
                        _ = sessionInfo;
                    }
                    catch
                    {
                    }

                    UpdateRepositoryStatus(address, ConnectionStatusOptions.Connected);

                    IBleDeviceService? previousDevice;
                    Boolean hadPrevious = _trackedDeviceByAddress.TryRemove(address, out previousDevice);
                    Action<ConnectionStatusOptions>? previousHandler;
                    Boolean hadPreviousHandler = _statusHandlerByAddress.TryRemove(address, out previousHandler);
                    if (hadPrevious && previousDevice != null && hadPreviousHandler && previousHandler != null)
                    {
                        try { previousDevice.ConnectionStatusChanged -= previousHandler; } catch { }
                        try { previousDevice.Dispose(); } catch { }
                    }
                    Action<ConnectionStatusOptions> statusChangedHandler = (ConnectionStatusOptions status) =>
                    {
                        DeviceLinkStatusChangedEventArgs changedEvent = new()
                        {
                            Address = address,
                            Status = status,
                            TimestampUtc = DateTime.UtcNow
                        };
                        _eventBus.Publish(changedEvent);
                        UpdateRepositoryStatus(address, status);
                    };
                    try { candidateDevice.ConnectionStatusChanged += statusChangedHandler; } catch { }
                    _statusHandlerByAddress[address] = statusChangedHandler;
                    _trackedDeviceByAddress[address] = candidateDevice;

                    DeviceLinkStatusChangedEventArgs initialEvent = new()
                    {
                        Address = address,
                        Status = ConnectionStatusOptions.Connected,
                        TimestampUtc = DateTime.UtcNow
                    };
                    _eventBus.Publish(initialEvent);

                    return true;
                }
                finally
                {
                    try { connectionGate.Release(); } catch { }
                }
            }
        }

        public async Task<Boolean> ProbeSessionAsync(UInt64 address, CancellationToken cancellationToken)
        {
            IBleDeviceService? device;
            Boolean found = _trackedDeviceByAddress.TryGetValue(address, out device);
            Boolean createdNew = false;
            if (!found || device == null)
            {
                device = await _provider.FromAddressAsync(address, cancellationToken);
                if (device == null)
                {
                    return false;
                }
                createdNew = true;
            }
            try
            {
                GattSessionInfo info = await device.GetGattSessionInfoAsync(cancellationToken);
                _ = info;
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (createdNew && device != null)
                {
                    try { device.Dispose(); } catch { }
                }
            }
        }

        public void Disconnect(UInt64 address)
        {
            UpdateRepositoryStatus(address, ConnectionStatusOptions.Disconnected);

            DeviceLinkStatusChangedEventArgs changedEvent = new()
            {
                Address = address,
                Status = ConnectionStatusOptions.Disconnected,
                TimestampUtc = DateTime.UtcNow
            };
            _eventBus.Publish(changedEvent);

            IBleDeviceService? device;
            Boolean found = _trackedDeviceByAddress.TryRemove(address, out device);
            Action<ConnectionStatusOptions>? handler;
            Boolean hadHandler = _statusHandlerByAddress.TryRemove(address, out handler);

            if (found && device != null && hadHandler && handler != null)
            {
                try { device.ConnectionStatusChanged -= handler; } catch { }
            }
            if (found && device != null)
            {
                try { device.Dispose(); } catch { }
            }
        }

        private void UpdateRepositoryStatus(UInt64 address, ConnectionStatusOptions status)
        {
            DeviceState? state = _deviceRepository.TryGetByAddress(address);
            if (state != null)
            {
                state.ConnectionStatus = status;
                if (status == ConnectionStatusOptions.Connected || status == ConnectionStatusOptions.Disconnected)
                {
                    state.LastSeenUtc = DateTime.UtcNow;
                }
                _deviceRepository.Upsert(state);
                DeviceUpsertedEventArgs upserted = new() { Device = state, IsNew = false };
                _eventBus.Publish(upserted);
            }
        }
    }
}