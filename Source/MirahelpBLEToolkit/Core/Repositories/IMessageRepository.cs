using MirahelpBLEToolkit.Core.Models;
using System;
using System.Collections.Generic;

namespace MirahelpBLEToolkit.Core.Interfaces
{
    public interface IMessageRepository
    {
        void Add(UInt64 address, MessageRecord messageRecord);
        IReadOnlyList<MessageRecord> GetLatest(UInt64 address, MessageQuery query);
        MessageRecord? TryGetById(UInt64 address, Int64 id);
        void Clear(UInt64 address);
        void SetRetention(Int32 maxPerDevice);
    }
}