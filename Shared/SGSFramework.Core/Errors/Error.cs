// ==========================================
// 檔案路徑: src/Core/SGSFramework.Core/Errors/Error.cs
// 架構層級: Domain Layer
// ==========================================

namespace SGSFramework.Core.Errors;

using System;

/// <summary>
/// 企業級領域錯誤物件，提供不可變之強型別錯誤描述與靜態工廠建構方法。
/// </summary>
public record Error
{
    public string Code { get; }
    public string Message { get; }
    public ErrorType Type { get; }

    protected Error(string code, string message, ErrorType type)
    {
        Code = string.IsNullOrWhiteSpace(code)
            ? throw new ArgumentNullException(nameof(code), "錯誤代碼不得為空。")
            : code;
        Message = string.IsNullOrWhiteSpace(message)
            ? throw new ArgumentNullException(nameof(message), "錯誤訊息不得為空。")
            : message;
        Type = type;
    }

    /// <summary>代表無錯誤之預設狀態實例</summary>
    public static readonly Error None = new("System.None", "無錯誤。", ErrorType.Failure);

    #region 依ErrorType 分別建立個錯誤類別
    /// <summary>
    /// 一般業務操作失敗
    /// </summary>
    /// <param name="code"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);

    /// <summary>
    /// 輸入參數或欄位驗證失敗 (HTTP 400 Bad Request)
    /// </summary>
    /// <param name="code"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    /// <summary>
    /// 指定之資源或實體不存在 (HTTP 404 Not Found)
    /// </summary>
    /// <param name="code"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    /// <summary>
    /// 資源狀態衝突或重複建立 (HTTP 409 Conflict) 
    /// </summary>
    /// <param name="code"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);
    
    /// <summary>
    /// 認證失敗 (HTTP 401 Unauthorized) 
    /// </summary>
    /// <param name="code"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);
    
    /// <summary>
    /// 權限不足 (HTTP 403 Forbidden) 
    /// </summary>
    /// <param name="code"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);
    
    /// <summary>
    /// 未處理的異常 (HTTP 500 Internal Server Error)
    /// </summary>
    /// <param name="code"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    public static Error Unprocessable(string code, string message) => new(code, message, ErrorType.Unprocessable);
    
    /// <summary>
    /// 依賴服務失敗 (HTTP 502 Bad Gateway)
    /// </summary>
    /// <param name="code"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    public static Error DependencyFailure(string code, string message) => new(code, message, ErrorType.DependencyFailure);
    
    /// <summary>
    /// 服務不可用 (HTTP 503 Service Unavailable)
    /// </summary>
    /// <param name="code"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    public static Error Unexpected(string code, string message) => new(code, message, ErrorType.Unexpected);

    #endregion

}