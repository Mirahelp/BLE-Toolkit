using MirahelpBLEToolkit.Core.Enums;
using System.Threading;
using System.Threading.Tasks;

namespace MirahelpBLEToolkit.Core.Interfaces
{
    public interface IDiscoveryService
    {
        void Start(ScanModeOptions mode);
        void Stop();
        Task RefreshPairedAsync(CancellationToken cancellationToken);
    }
}