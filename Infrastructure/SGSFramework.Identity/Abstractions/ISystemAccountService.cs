using SGSFramework.Identity.DTOs;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SGSFramework.Identity.Abstractions
{
    /// <summary>
    /// 系統帳號（M2M）管理與權杖換發服務
    /// </summary>
    public interface ISystemAccountService
    {
        Task<IReadOnlyList<SystemAccountDto>> GetAccountsAsync(CancellationToken cancellationToken = default);

        Task<SystemAccountDto?> GetAccountAsync(string id, CancellationToken cancellationToken = default);

        /// <summary>建立系統帳號並產生金鑰（明文僅回傳一次）</summary>
        /// <exception cref="System.ArgumentException">欄位格式無效或識別碼重複</exception>
        Task<SystemAccountSecretResultDto> CreateAsync(CreateSystemAccountRequest request, string? clientIp, CancellationToken cancellationToken = default);

        /// <exception cref="KeyNotFoundException">帳號不存在</exception>
        /// <exception cref="System.ArgumentException">欄位格式無效</exception>
        /// <exception cref="System.InvalidOperationException">帳號凍結中</exception>
        Task<SystemAccountDto> UpdateAsync(string id, UpdateSystemAccountRequest request, string? clientIp, CancellationToken cancellationToken = default);

        /// <summary>整批取代操作範圍；異動後既有權杖立即失效，用戶端以新 Scope 重新換發</summary>
        Task<SystemAccountDto> UpdateScopesAsync(string id, UpdateSystemAccountScopesRequest request, string? clientIp, CancellationToken cancellationToken = default);

        /// <summary>變更狀態（停用 / 凍結時既有權杖立即失效）</summary>
        Task<SystemAccountDto> ChangeStatusAsync(string id, ChangeSystemAccountStatusRequest request, string? clientIp, CancellationToken cancellationToken = default);

        /// <summary>輪替金鑰（新金鑰明文僅回傳一次）</summary>
        Task<SystemAccountSecretResultDto> RotateSecretAsync(string id, RotateSystemAccountSecretRequest request, string? clientIp, CancellationToken cancellationToken = default);

        /// <summary>撤銷所有已簽發權杖（金鑰不變，用戶端會自動重新換發）</summary>
        Task<SystemAccountDto> RevokeTokensAsync(string id, RevokeSystemAccountTokensRequest request, string? clientIp, CancellationToken cancellationToken = default);

        /// <summary>刪除帳號（僅限停用狀態），稽核歷程保留</summary>
        Task DeleteAsync(string id, string? clientIp, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<SystemAccountAuditLogDto>> GetAuditLogsAsync(string? accountId, int take, CancellationToken cancellationToken = default);

        /// <summary>OAuth 2.0 Client Credentials：以 client_id + client_secret 換發短效 JWT</summary>
        Task<SystemTokenIssueResult> IssueTokenAsync(SystemTokenRequest request, string? clientIp, CancellationToken cancellationToken = default);
    }
}
