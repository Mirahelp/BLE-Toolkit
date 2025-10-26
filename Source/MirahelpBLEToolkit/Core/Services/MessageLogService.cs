using MirahelpBLEToolkit.Core.Interfaces;
using MirahelpBLEToolkit.Core.Models;
using System;
using System.Collections.Generic;

namespace MirahelpBLEToolkit.Core.Services
{
    public sealed class MessageLogService : IMessageLogService
    {
        private readonly IMessageRepository _repository;

        public MessageLogService(IMessageRepository repository)
        {
            _repository = repository;
        }

        public void Append(MessageRecord record)
        {
            _repository.Add(record.Address, record);
        }

        public void Clear(UInt64 address)
        {
            _repository.Clear(address);
        }

        public IReadOnlyList<MessageRecord> Query(MessageQuery query)
        {
            IReadOnlyList<MessageRecord> list = _repository.GetLatest(query.Address, query);
            return list;
        }

        public void SetRetention(Int32 maxPerDevice)
        {
            _repository.SetRetention(maxPerDevice);
        }
    }
}
