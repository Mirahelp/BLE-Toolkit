using MirahelpBLEToolkit.Core.Models;
using MirahelpBLEToolkit.Core.Results;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MirahelpBLEToolkit.Core.Interfaces
{
    public interface IHeartbeatService
    {
        void Start();
        void Stop();
        void SetEnabled(UInt64 address, Boolean isEnabled);
        Boolean IsEnabled(UInt64 address);
        void SetPeriod(UInt64 address, Int32 seconds);
        Int32 GetPeriod(UInt64 address);
        DeviceHeartbeatSnapshot GetSnapshot(UInt64 address);
        Task<HeartbeatProbeResult> ProbeNowAsync(UInt64 address, CancellationToken cancellationToken);
    }
}