using System.Diagnostics;
using System.Globalization;

namespace Enterprise.HttpSdk.Handlers
{
    /// <summary>
    /// 對外呼叫時附加 X-Correlation-ID，讓下游服務的日誌能與本系統同一次請求串接追朔。
    /// </summary>
    /// <remarks>
    /// 追蹤碼來源（依序）：
    /// 1. 目前請求的追蹤碼：由 CorrelationIdMiddleware 以 Activity Baggage（<see cref="BaggageKey"/>）帶入，
    ///    與系統日誌、資安日誌、資料日誌使用同一個值；
    /// 2. 目前 Activity 的 W3C TraceId；
    /// 3. 全新 GUID。
    /// 格式固定為 32 碼小寫十六進位（不含連字號）。本類別刻意不依賴 SGSFramework.Core，以保持此 SDK 可獨立使用。
    /// </remarks>
    public class ClientCorrelationHandler : DelegatingHandler
    {
        public const string HeaderName = "X-Correlation-ID";
        public const string BaggageKey = "correlation_id";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (!request.Headers.Contains(HeaderName))
            {
                request.Headers.Add(HeaderName, ResolveCorrelationId());
            }

            return await base.SendAsync(request, cancellationToken);
        }

        /// <summary>
        /// 取得目前請求的追蹤碼；預設無（改由 Activity 推導）。
        /// 在可存取請求上下文的專案中覆寫此方法（如 SGSFramework.Core 的 CorrelationContext），
        /// 不依賴 Activity 是否被建立，結果最穩定。
        /// </summary>
        protected virtual string? GetCurrentCorrelationId() => null;

        private string ResolveCorrelationId()
        {
            var fromContext = Normalize(GetCurrentCorrelationId());
            if (fromContext is not null) return fromContext;

            var activity = Activity.Current;

            var fromBaggage = Normalize(activity?.GetBaggageItem(BaggageKey));
            if (fromBaggage is not null) return fromBaggage;

            if (activity is { IdFormat: ActivityIdFormat.W3C } && activity.TraceId != default)
            {
                return activity.TraceId.ToHexString();
            }

            return Guid.NewGuid().ToString("N");
        }

        protected static string? Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            var trimmed = value.Trim();
            if (Guid.TryParse(trimmed, out var guid)) return guid.ToString("N");

            return trimmed.Length == 32 && trimmed.All(Uri.IsHexDigit)
                ? trimmed.ToLower(CultureInfo.InvariantCulture)
                : null;
        }
    }
}
