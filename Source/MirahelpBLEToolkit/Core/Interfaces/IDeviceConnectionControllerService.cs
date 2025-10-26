using MirahelpBLEToolkit.Core.Models;
using System;

namespace MirahelpBLEToolkit.Core.Interfaces
{
    public interface IDeviceConnectionControllerService
    {
        DeviceConnectionSnapshot GetSnapshot();
        Guid RequestConnect();
        void RequestDisconnect(Boolean isManual);
        void SetAutoReconnectEnabled(Boolean isEnabled);
    }
}