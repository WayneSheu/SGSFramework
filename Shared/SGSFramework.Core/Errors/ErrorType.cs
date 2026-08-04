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
        Failure = 0,// 一般失敗，適用於未知錯誤或不特定類型的錯誤
        Validation = 1,// 驗證失敗，適用於輸入資料不符合規範或商業邏輯驗證失敗的情況
        NotFound = 2,// 未找到，適用於請求的資源不存在或無法找到的情況
        Conflict = 3,// 衝突，適用於請求的操作與現有資源狀態衝突的情況，例如嘗試創建已存在的資源或更新已被修改的資源
        Unauthorized = 4// 未授權，適用於請求缺乏有效的身份驗證或授權資訊的情況
    }
}
