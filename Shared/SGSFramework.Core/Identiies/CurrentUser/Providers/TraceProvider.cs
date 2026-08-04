using System.Diagnostics;

namespace SGSFramework.Core.Identiies.CurrentUser.Providers
{
    public static class TraceProvider
    {
        /// <summary>
        /// 取得當前的 W3C TraceId。如果沒有活動中的 Activity，則產生一個新的關聯 ID。
        /// </summary>
        public static string GetCurrentTraceId()
        {
            // 1. 優先嘗試取得標準 W3C TraceId (32位元 hex 字串)
            if (Activity.Current != null)
            {
                return Activity.Current.TraceId.ToString();
            }

            // 2. 如果是背景服務或非 HTTP 請求，回退方案：
            // 建議：使用與 W3C 格式相容的 GUID (移除 dash)
            return Guid.NewGuid().ToString("N");
        }
    }
}
