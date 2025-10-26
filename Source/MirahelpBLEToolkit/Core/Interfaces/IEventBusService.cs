using System;

namespace MirahelpBLEToolkit.Core.Interfaces
{
    public interface IEventBusService
    {
        void Publish<T>(T evt);
        IDisposable Subscribe<T>(Action<T> handler);
    }
}