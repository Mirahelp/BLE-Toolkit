using MirahelpBLEToolkit.Core.Models;
using System;
using System.Collections.Generic;

namespace MirahelpBLEToolkit.Core.Interfaces
{
    public interface IMessageLogService
    {
        void Append(MessageRecord record);
        IReadOnlyList<MessageRecord> Query(MessageQuery query);
        void Clear(UInt64 address);
        void SetRetention(Int32 maxPerDevice);
    }
}