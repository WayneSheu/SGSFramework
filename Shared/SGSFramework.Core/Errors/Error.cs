using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Errors
{
    /// <summary>
    /// 錯誤類別 (Error Class)
    /// </summary>
    public record Error
    {
        public string Code { get; }
        public string Message { get; }
        public ErrorType Type { get; }

        protected Error(string code, string message, ErrorType type)
        {
            Code = code;
            Message = message;
            Type = type;
        }

        // 預設的空錯誤實例，表示沒有錯誤
        public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);
        // 靜態工廠方法，根據錯誤類型創建不同類型的錯誤實例
        // 這些方法提供了一個簡潔的方式來創建特定類型的錯誤實例，並且可以根據需要擴展以支持更多的錯誤類型

        /// <summary>
        ///  一般業務邏輯失敗 (400)
        /// </summary>
        /// <param name="code"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);

        /// <summary>
        /// 未預期的系統內部錯誤 (500)
        /// </summary>
        public static Error Unexpected(string code, string message) => new(code, message, ErrorType.Unexpected);


        /// <summary>
        /// 驗證失敗
        /// </summary>
        /// <param name="code"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);
        
        /// <summary>
        /// 未找到資源
        /// </summary>
        /// <param name="code"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);
        
        /// <summary>
        /// 資源已存在
        /// </summary>
        /// <param name="code"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);
        
        /// <summary>
        /// 未經授權的訪問
        /// </summary>
        /// <param name="code"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);
    }
}
