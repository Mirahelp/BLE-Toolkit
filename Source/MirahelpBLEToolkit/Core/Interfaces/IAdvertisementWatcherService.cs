using MirahelpBLEToolkit.Core.Events;
using System;

namespace MirahelpBLEToolkit.Core.Interfaces
{
    public interface IAdvertisementWatcherService
    {
        event Action<AdvertisementReceivedEventArgs>? Received;
        void Start();
        void Stop();
    }
}
