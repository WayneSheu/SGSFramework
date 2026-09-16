using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace SGSFramework.Core.Errors
{
    /// <summary>
    /// 表示複合式欄位驗證錯誤之領域物件，繼承自 Error 並包含多筆細項驗證錯誤清單。
    /// </summary>
    public sealed record ValidationError : Error
    {
        public IReadOnlyCollection<Error> Errors { get; }

        private ValidationError(IReadOnlyCollection<Error> errors)
            : base("Validation.General", "發生一項或多項驗證錯誤。", ErrorType.Validation)
        {
            Errors = errors ?? new ReadOnlyCollection<Error>(Array.Empty<Error>());
        }

        /// <summary>
        /// 由細項 Error 集合建構 ValidationError 實例
        /// </summary>
        public static ValidationError FromErrors(IReadOnlyCollection<Error> errors)
        {
            ArgumentNullException.ThrowIfNull(errors);
            return new ValidationError(errors);
        }
    }
}
