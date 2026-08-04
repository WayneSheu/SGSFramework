using System;
using System.Collections.Generic;
using System.Text;

namespace Enterprise.HttpSdk.Dtos
{
    public sealed class ActionResponse
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
