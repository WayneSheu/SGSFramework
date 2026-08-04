using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class OrderAttribute : Attribute
    {
        public int Order { get; }
        public OrderAttribute(int order) => Order = order;
    }
}
