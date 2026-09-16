namespace SGSFramework.Core.Results;

using System;
using System.Threading.Tasks;

/// <summary>
/// 提供 Result 與 Result<TValue> 之函數式操作擴充（鏈結轉換、條件判斷）
/// 歸屬於 SGSFramework.Core 專案
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// 成功時執行數值轉換 (Map)
    /// </summary>
    public static Result<TOut> Map<TIn, TOut>(this Result<TIn> result, Func<TIn, TOut> func)
    {
        ArgumentNullException.ThrowIfNull(func);

        return result.IsSuccess
            ? Result.Success(func(result.Value))
            : Result.Failure<TOut>(result.Error);
    }

    /// <summary>
    /// 成功時執行非同步鏈結操作 (Bind)
    /// </summary>
    public static async Task<Result<TOut>> BindAsync<TIn, TOut>(
        this Result<TIn> result,
        Func<TIn, Task<Result<TOut>>> func)
    {
        ArgumentNullException.ThrowIfNull(func);

        return result.IsSuccess
            ? await func(result.Value).ConfigureAwait(false)
            : Result.Failure<TOut>(result.Error);
    }
}