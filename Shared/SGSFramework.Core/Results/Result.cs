
namespace SGSFramework.Core.Results;

using SGSFramework.Core.Errors;
using System;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// 表示領域操作之執行狀態容器，明確隔離成功與失敗路徑。
/// </summary>
public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None || !isSuccess && error == Error.None)
        {
            throw new ArgumentException("無效之成功狀態與錯誤物件組合。", nameof(error));
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
/// 表示包含回傳資料之領域操作執行狀態容器。
/// </summary>
/// <typeparam name="TValue">資料型別</typeparam>
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
        : throw new InvalidOperationException("無法從失敗的 Result 中讀取 Value 屬性。");

    // 隱式轉換糖 (Implicit Operators)
    public static implicit operator Result<TValue>(TValue? value) =>
        value is not null
            ? Success(value)
            : Failure<TValue>(Error.NotFound("Value.Null", "回傳之實體數值為空 (Null)。"));

    public static implicit operator Result<TValue>(Error error) => Failure<TValue>(error);
}