using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ApiVersionAttribute : Attribute
    {
        public string Version { get; }
        public ApiVersionAttribute(string version) => Version = version;
    }
}
