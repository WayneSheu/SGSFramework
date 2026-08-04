using SGSFramework.Core.Utilities;
using System;


namespace SGSFramework.Core.Abstractions.Events
{
    public abstract class EventMessage
    {
        public string MessageType { get; protected set; }

        public Guid AggregateId { get; protected set; }

        protected EventMessage()
        {
            MessageType = GetType().GetGenericTypeName();
        }
    }
}