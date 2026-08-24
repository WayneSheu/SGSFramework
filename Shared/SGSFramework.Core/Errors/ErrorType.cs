using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Errors
{
    /// <summary>
    /// 
    /// </summary>
    public enum ErrorType
    {
        Failure = 0,      // 一般業務失敗 (400 Bad Request)[cite: 9]
        Validation = 1,   // 輸入驗證失敗 (400 Bad Request)[cite: 9]
        NotFound = 2,     // 資源不存在 (404 Not Found)[cite: 9]
        Conflict = 3,     // 狀態衝突 (409 Conflict)[cite: 9]
        Unauthorized = 4, // 身份驗證/授權失敗 (401/403)[cite: 9]
        Unexpected = 5    // 未預期系統異常 (500 Internal Server Error)
    }
}
