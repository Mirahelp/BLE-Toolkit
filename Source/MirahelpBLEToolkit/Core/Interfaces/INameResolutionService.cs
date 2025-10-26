using System;
using System.Threading;
using System.Threading.Tasks;

namespace MirahelpBLEToolkit.Core.Interfaces
{
    public interface INameResolutionService
    {
        void Start();
        void Stop();
        void Enqueue(UInt64 address);
        Task FetchNowAsync(UInt64 address, CancellationToken cancellationToken);
    }
}