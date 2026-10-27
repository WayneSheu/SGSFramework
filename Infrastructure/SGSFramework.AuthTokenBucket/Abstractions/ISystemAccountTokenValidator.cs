using System;
using System.Threading;
using System.Threading.Tasks;

namespace SGSFramework.AuthTokenBucket.Abstractions
{
    /// <summary>
    /// 系統帳號權杖即時有效性檢查（JwtBearer OnTokenValidated 呼叫）。
    /// 帳號停用 / 凍結 / 刪除，或權杖簽發時間早於撤銷時間點時，已簽發但尚未過期的權杖立即失效。
    /// </summary>
    public interface ISystemAccountTokenValidator
    {
        /// <summary>權杖仍有效時回傳 null，否則回傳拒絕原因</summary>
        Task<string?> ValidateAsync(string accountId, DateTimeOffset issuedAt, CancellationToken cancellationToken = default);
    }
}
