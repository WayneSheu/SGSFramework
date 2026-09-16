// ==========================================
// 檔案路徑: src/Core/SGSFramework.Core/Errors/ErrorType.cs
// 架構層級: Domain / Core Layer
// ==========================================

namespace SGSFramework.Core.Errors
{
    /// <summary>
    /// 定義領域與業務邏輯層之錯誤類型列舉，
    /// 供 Result 與 Error 物件進行錯誤分類，並作為 Web 轉譯層對映 HTTP Status Code 之依據。
    /// </summary>
    public enum ErrorType
    {
        /// <summary>一般業務操作失敗 (HTTP 400 Bad Request)</summary>
        Failure = 0,

        /// <summary>輸入參數或欄位驗證失敗 (HTTP 400 Bad Request)</summary>
        Validation = 1,

        /// <summary>指定之資源或實體不存在 (HTTP 404 Not Found)</summary>
        NotFound = 2,

        /// <summary>資源狀態衝突或重複建立 (HTTP 409 Conflict)</summary>
        Conflict = 3,

        /// <summary>未提供有效身分憑證或未登入 (HTTP 401 Unauthorized)</summary>
        Unauthorized = 4,

        /// <summary>已驗證身分但缺乏存取權限/越權存取 (HTTP 403 Forbidden)</summary>
        Forbidden = 5,

        /// <summary>語法正確但違反領域業務規則或前置條件 (HTTP 422 Unprocessable Entity)</summary>
        Unprocessable = 6,

        /// <summary>第三方 API 或外部基礎設施服務呼叫失敗 (HTTP 502 Bad Gateway)</summary>
        DependencyFailure = 7,

        /// <summary>未預期的系統內部例外與未捕捉異常 (HTTP 500 Internal Server Error)</summary>
        Unexpected = 8
    }
}