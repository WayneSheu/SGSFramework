using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Errors
{
    /// <summary>
    /// ValidationError 類別用於表示驗證錯誤。它繼承自 Error 類別，並包含一個 Error 物件的集合。
    /// </summary>
    public sealed record ValidationError : Error
    {
        public IReadOnlyCollection<Error> Errors { get; }

        private ValidationError(IReadOnlyCollection<Error> errors)
            : base("Validation.General", "發生一項或多項驗證錯誤。", ErrorType.Validation)
        {
            Errors = errors;
        }

        public static ValidationError FromErrors(IReadOnlyCollection<Error> errors) => new(errors);
    }
}
