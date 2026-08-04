
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using MediatR;

namespace SGSFramework.Core.Abstractions.Events
{
    public abstract class Event : EventMessage, INotification
    {
        [NotMapped]
        [Newtonsoft.Json.JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public IEnumerable<Type> RelatedEntities { get; protected set; }

        public DateTime Timestamp { get; private set; }

        public string EventDescription { get; set; }

        protected Event(string description = null)
        {
            Timestamp = DateTime.Now;
            if (string.IsNullOrWhiteSpace(description))
            {
                EventDescription = description;
            }
        }
    }
}