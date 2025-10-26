using MirahelpBLEToolkit.Core.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MirahelpBLEToolkit.Core.Interfaces
{
    public interface IOperationQueueService
    {
        Guid Enqueue(OperationRequest request, Func<CancellationToken, Task<OperationResult>> work);
        Boolean TryCancel(Guid operationId);
        OperationSnapshot GetSnapshot();
        event Action<OperationProgress>? Progressed;
    }
}