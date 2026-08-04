using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.CodeSecurity.Abstractions
{
    /// <summary>
    /// 簽署處理狀態
    /// </summary>
    public enum SigningStatus { Pending, Success, Failed, ValidationError }
}
