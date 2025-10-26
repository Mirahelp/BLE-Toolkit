using MirahelpBLEToolkit.Core.Models;
using System;

namespace MirahelpBLEToolkit.Core.Interfaces
{
    public interface IConnectionOrchestratorRegistryService
    {
        IDeviceConnectionControllerService Get(UInt64 address);
        DeviceConnectionSnapshot GetSnapshot(UInt64 address);
        Guid RequestConnect(UInt64 address);
        void RequestDisconnect(UInt64 address, Boolean isManual);
        void SetAutoReconnectEnabled(UInt64 address, Boolean isEnabled);
    }
}