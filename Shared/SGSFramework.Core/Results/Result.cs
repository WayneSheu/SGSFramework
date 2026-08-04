using SGSFramework.Core.Errors;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace SGSFramework.Core.Results
{
    /// <summary>
    /// Result 類別用於表示操作的結果狀態，包含成功與失敗兩種情況。
    /// 它提供了 IsSuccess 和 IsFailure 屬性來判斷操作是否成功，以及一個 Error 屬性來描述失敗的錯誤資訊。
    /// </summary>
    public class Result
    {
        protected Result(bool isSuccess, Error error)
        {
            if (isSuccess && error != Error.None || !isSuccess && error == Error.None)
            {
                throw new ArgumentException("無效的錯誤與成功狀態組合。", nameof(error));
            }

            IsSuccess = isSuccess;
            Error = error;
        }

        public bool IsSuccess { get; }
        public bool IsFailure => !IsSuccess;
        public Error Error { get; }

        public static Result Success() => new(true, Error.None);
        public static Result Failure(Error error) => new(false, error);
        public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);
        public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);
    }

    /// <summary>
    /// Result<TValue> 類別用於表示操作的結果狀態，包含成功與失敗兩種情況。
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    public class Result<TValue> : Result
    {
        private readonly TValue? _value;

        protected internal Result(TValue? value, bool isSuccess, Error error)
            : base(isSuccess, error)
        {
            _value = value;
        }

        [NotNull]
        public TValue Value => IsSuccess
            ? _value!
            : throw new InvalidOperationException("無法從失敗的結果中獲取數值。");

        // 生產級關鍵：隱式轉換糖
        public static implicit operator Result<TValue>(TValue? value) =>
            value is not null ? Success(value) : Failure<TValue>(Error.NotFound("Value.Null", "回傳的數值為空。"));

        public static implicit operator Result<TValue>(Error error) => Failure<TValue>(error);
    }
}
