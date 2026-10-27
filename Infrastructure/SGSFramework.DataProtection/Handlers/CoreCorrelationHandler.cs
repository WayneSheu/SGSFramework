using Enterprise.HttpSdk.Handlers;
using SGSFramework.Core.Tracing;

namespace SGSFramework.DataProtection.Handlers
{
    /// <summary>
    /// 對外呼叫（如 HMS）附加與本系統日誌相同的追蹤碼：直接取自 <see cref="CorrelationContext"/>，
    /// 不依賴 Activity 是否被建立。
    /// </summary>
    public sealed class CoreCorrelationHandler : ClientCorrelationHandler
    {
        protected override string? GetCurrentCorrelationId() => CorrelationContext.Current;
    }
}
