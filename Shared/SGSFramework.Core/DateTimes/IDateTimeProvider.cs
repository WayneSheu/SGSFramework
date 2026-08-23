using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.DateTimes
{
    public interface IDateTimeProvider
    {
        DateTimeOffset Now { get; }
        DateTime UtcNow { get; }
    }
}
