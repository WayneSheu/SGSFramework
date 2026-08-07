using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Entities.Base
{
    /// <summary>
    /// 定義擴充 Identity 使用者必須具備的自訂基礎欄位
    /// </summary>
    public interface IBaseUser
    { 
        /// <summary>
        /// 使用者的全名，這個欄位可以用來存儲使用者的完整名稱，方便在應用程式中顯示和識別使用者。
        /// </summary>
        string FullName { get; set; }
        /// <summary>
        /// 帳號最後登入時間，這個欄位可以用來追蹤使用者的最後一次登入時間，對於安全性和使用者行為分析非常有用。
        /// </summary>
        DateTimeOffset? LastLoginAt { get; set; }
        /// <summary>
        /// 
        /// </summary>
        DateTimeOffset CreatedAt { get; set; }
    }
}
