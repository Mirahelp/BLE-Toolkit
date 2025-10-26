using MirahelpBLEToolkit.Core.Events;
using MirahelpBLEToolkit.Core.Interfaces;
using MirahelpBLEToolkit.Core.Models;
using System;

namespace MirahelpBLEToolkit.Core.Services
{
    public sealed class SignalHistoryService : ISignalHistoryService
    {
        private readonly IEventBusService _eventBus;
        private readonly ISignalHistoryRepositoryService _repository;
        private IDisposable? _subscription;

        public SignalHistoryService(IEventBusService eventBus, ISignalHistoryRepositoryService repository)
        {
            _eventBus = eventBus;
            _repository = repository;
            _subscription = null;
        }

        public void Start()
        {
            _subscription = _eventBus.Subscribe<AdvertisementReceivedEventArgs>(OnAdvertisement);
        }

        public void Stop()
        {
            IDisposable? subscription = _subscription;
            if (subscription != null)
            {
                try { subscription.Dispose(); } catch { }
            }
            _subscription = null;
        }

        private void OnAdvertisement(AdvertisementReceivedEventArgs advertisementEventArgs)
        {
            RssiSample sample = new()
            {
                TimestampUtc = advertisementEventArgs.TimestampUtc,
                Rssi = advertisementEventArgs.Rssi
            };
            _repository.Append(advertisementEventArgs.Address, sample);
        }
    }
}