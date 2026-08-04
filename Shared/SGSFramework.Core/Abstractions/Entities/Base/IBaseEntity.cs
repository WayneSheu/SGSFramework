using SGSFramework.Core.Abstractions.Events;
using System.Collections.Generic;

namespace SGSFramework.Core.Abstractions.Entities.Base
{ 
    public interface IBaseEntity
    {
        public IReadOnlyCollection<Event> DomainEvents { get; }

        public void AddDomainEvent(Event domainEvent);

        public void RemoveDomainEvent(Event domainEvent);

        public void ClearDomainEvents();
    }
}