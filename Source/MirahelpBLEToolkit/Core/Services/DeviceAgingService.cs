using MirahelpBLEToolkit.Configuration;
using MirahelpBLEToolkit.Core.Enums;
using MirahelpBLEToolkit.Core.Interfaces;
using MirahelpBLEToolkit.Core.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MirahelpBLEToolkit.Core.Services
{
    public sealed class DeviceAgingService : IDeviceAgingService
    {
        private readonly IDeviceRepositoryService _deviceRepository;
        private readonly IClockService _clock;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _loopTask;

        public DeviceAgingService(IDeviceRepositoryService deviceRepository, IClockService clock)
        {
            _deviceRepository = deviceRepository;
            _clock = clock;
        }

        public void Start()
        {
            if (_loopTask != null)
            {
                return;
            }
            _cancellationTokenSource = new CancellationTokenSource();
            CancellationToken cancellationToken = _cancellationTokenSource.Token;
            _loopTask = Task.Run(async () =>
            {
                try
                {
                    using (PeriodicTimer periodicTimer = new(TimeSpan.FromSeconds(1)))
                    {
                        while (await periodicTimer.WaitForNextTickAsync(cancellationToken))
                        {
                            Int32 ttlSeconds = Math.Max(1, AppConfig.DeviceTtlSeconds);
                            DateTime nowUtc = _clock.UtcNow;
                            foreach (DeviceState device in _deviceRepository.GetAll().ToList())
                            {
                                if (device.Pinned)
                                {
                                    continue;
                                }
                                if (device.ConnectionStatus == ConnectionStatusOptions.Connected)
                                {
                                    continue;
                                }
                                TimeSpan age = nowUtc - device.LastSeenUtc;
                                if (age.TotalSeconds > ttlSeconds)
                                {
                                    _deviceRepository.Remove(device.Address);
                                }
                            }
                        }
                    }
                }
                catch
                {
                }
            }, cancellationToken);
        }

        public void Stop()
        {
            CancellationTokenSource? source = _cancellationTokenSource;
            if (source != null)
            {
                try
                {
                    source.Cancel();
                }
                catch
                {
                }
            }
            _cancellationTokenSource = null;
            _loopTask = null;
        }
    }
}