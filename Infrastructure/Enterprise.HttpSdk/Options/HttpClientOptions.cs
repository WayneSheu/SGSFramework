using System;
using System.Collections.Generic;
using System.Text;

namespace Enterprise.HttpSdk.Options
{

    public class HttpClientOptions
    {
        public Uri? BaseAddress { get; set; }
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
        public int MaxRetryAttempts { get; set; } = 3;
    }
}
