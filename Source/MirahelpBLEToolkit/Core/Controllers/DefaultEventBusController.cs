using MirahelpBLEToolkit.Core.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace MirahelpBLEToolkit.Core.Controllers
{
    public sealed class DefaultEventBusController : IEventBusService
    {
        private readonly ConcurrentDictionary<Type, List<Delegate>> _handlersByType = new();

        public void Publish<T>(T evt)
        {
            List<Delegate> registeredHandlers;
            Boolean hasHandlers = _handlersByType.TryGetValue(typeof(T), out registeredHandlers);
            if (!hasHandlers || registeredHandlers == null)
            {
                return;
            }
            List<Delegate> snapshot;
            lock (registeredHandlers)
            {
                snapshot = new List<Delegate>(registeredHandlers);
            }
            foreach (Delegate del in snapshot)
            {
                Action<T>? handler = del as Action<T>;
                if (handler != null)
                {
                    try
                    {
                        handler(evt);
                    }
                    catch
                    {
                    }
                }
            }
        }

        public IDisposable Subscribe<T>(Action<T> handler)
        {
            List<Delegate> list = _handlersByType.GetOrAdd(typeof(T), (Type eventType) => new List<Delegate>());
            lock (list)
            {
                list.Add(handler);
            }
            return new Unsubscriber(() =>
            {
                lock (list)
                {
                    list.Remove(handler);
                }
            });
        }

        private sealed class Unsubscriber : IDisposable
        {
            private readonly Action _disposeAction;
            private Boolean _isDisposed;

            public Unsubscriber(Action disposeAction)
            {
                _disposeAction = disposeAction;
                _isDisposed = false;
            }

            public void Dispose()
            {
                if (_isDisposed)
                {
                    return;
                }
                _isDisposed = true;
                _disposeAction();
            }
        }
    }
}