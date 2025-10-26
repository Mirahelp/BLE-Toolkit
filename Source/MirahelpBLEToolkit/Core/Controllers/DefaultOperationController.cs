using MirahelpBLEToolkit.Core.Interfaces;
using MirahelpBLEToolkit.Core.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MirahelpBLEToolkit.Core.Controllers
{
    public sealed class DefaultOperationController : IOperationQueueService
    {
        private readonly ConcurrentDictionary<String, Guid> _scopeToOperationIdentifier = new();
        private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _cancellationsByOperation = new();
        private readonly ConcurrentDictionary<Guid, OperationProgress> _progressByOperation = new();

        public event Action<OperationProgress>? Progressed;

        public Guid Enqueue(OperationRequest request, Func<CancellationToken, Task<OperationResult>> work)
        {
            CancellationTokenSource cancellationTokenSource = new();
            Guid operationIdentifier = Guid.NewGuid();
            _scopeToOperationIdentifier.AddOrUpdate(request.Scope, operationIdentifier, (String s, Guid g) => operationIdentifier);
            _cancellationsByOperation[operationIdentifier] = cancellationTokenSource;
            OperationProgress initial = new()
            {
                OperationId = operationIdentifier,
                Scope = request.Scope,
                Text = request.Title,
                Visible = true,
                Active = true
            };
            _progressByOperation[operationIdentifier] = initial;
            Action<OperationProgress>? progressedHandler = Progressed;
            if (progressedHandler != null)
            {
                progressedHandler(initial);
            }
            Task.Run(async () =>
            {
                try
                {
                    if (request.Timeout > TimeSpan.Zero)
                    {
                        cancellationTokenSource.CancelAfter(request.Timeout);
                    }
                    OperationResult result = await work(cancellationTokenSource.Token);
                    OperationProgress final = new()
                    {
                        OperationId = operationIdentifier,
                        Scope = request.Scope,
                        Text = result.Message,
                        Visible = true,
                        Active = false
                    };
                    _progressByOperation[operationIdentifier] = final;
                    Action<OperationProgress>? h = Progressed;
                    if (h != null)
                    {
                        h(final);
                    }
                }
                catch (Exception exception)
                {
                    OperationProgress final = new()
                    {
                        OperationId = operationIdentifier,
                        Scope = request.Scope,
                        Text = exception.Message,
                        Visible = true,
                        Active = false
                    };
                    _progressByOperation[operationIdentifier] = final;
                    Action<OperationProgress>? h = Progressed;
                    if (h != null)
                    {
                        h(final);
                    }
                }
                finally
                {
                    Guid removedOperation;
                    _scopeToOperationIdentifier.TryRemove(request.Scope, out removedOperation);
                    CancellationTokenSource removedCts;
                    _cancellationsByOperation.TryRemove(operationIdentifier, out removedCts);
                }
            });
            return operationIdentifier;
        }

        public Boolean TryCancel(Guid operationId)
        {
            CancellationTokenSource source;
            Boolean found = _cancellationsByOperation.TryGetValue(operationId, out source);
            if (found && source != null)
            {
                try
                {
                    source.Cancel();
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            return false;
        }

        public OperationSnapshot GetSnapshot()
        {
            List<OperationProgress> list = new(_progressByOperation.Values);
            OperationSnapshot snapshot = new()
            {
                Active = list
            };
            return snapshot;
        }
    }
}