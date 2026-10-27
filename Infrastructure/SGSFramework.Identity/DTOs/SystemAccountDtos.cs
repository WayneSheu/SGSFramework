using SGSFramework.Core.Abstractions.Entities.SystemAccounts;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SGSFramework.Identity.DTOs
{
    /// <summary>
    /// 系統帳號（不含金鑰雜湊）
    /// </summary>
    public sealed class SystemAccountDto
    {
        public string Id { get; init; } = string.Empty;
        public string ClientId { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
        public SystemAccountStatus Status { get; init; }
        public IReadOnlyList<string> Scopes { get; init; } = [];
        public int AccessTokenLifetimeMinutes { get; init; }
        public string? AllowedIpRanges { get; init; }

        /// <summary>金鑰末四碼（例如 …a1B2），供比對部署中的金鑰版本</summary>
        public string? SecretHint { get; init; }
        public DateTimeOffset? SecretExpiresAt { get; init; }
        public DateTimeOffset SecretRotatedAt { get; init; }
        public DateTimeOffset TokensNotBefore { get; init; }
        public DateTimeOffset? LastUsedAt { get; init; }
        public string? LastUsedIp { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public string? CreatedBy { get; init; }
        public DateTimeOffset? UpdatedAt { get; init; }
        public string? UpdatedBy { get; init; }
    }

    /// <summary>
    /// 建立系統帳號
    /// </summary>
    public sealed class CreateSystemAccountRequest
    {
        /// <summary>帳號代碼（小寫英數與底線），識別碼為 sys_{代碼}，建立後不可變更</summary>
        [Required(ErrorMessage = "請輸入帳號代碼。")]
        public string Code { get; set; } = string.Empty;

        /// <summary>用戶端識別碼 client_id（小寫英數與連字號），建立後不可變更</summary>
        [Required(ErrorMessage = "請輸入用戶端識別碼。")]
        public string ClientId { get; set; } = string.Empty;

        [Required(ErrorMessage = "請輸入顯示名稱。")]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public List<string> Scopes { get; set; } = [];

        [Range(5, 60, ErrorMessage = "權杖有效時間必須介於 5 到 60 分鐘之間。")]
        public int AccessTokenLifetimeMinutes { get; set; } = 15;

        public string? AllowedIpRanges { get; set; }

        /// <summary>金鑰有效天數（null 表示不到期）</summary>
        [Range(1, 3650)]
        public int? SecretValidDays { get; set; } = 365;
    }

    /// <summary>
    /// 更新系統帳號基本設定
    /// </summary>
    public sealed class UpdateSystemAccountRequest
    {
        [Required(ErrorMessage = "請輸入顯示名稱。")]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [Range(5, 60, ErrorMessage = "權杖有效時間必須介於 5 到 60 分鐘之間。")]
        public int AccessTokenLifetimeMinutes { get; set; } = 15;

        public string? AllowedIpRanges { get; set; }
    }

    /// <summary>
    /// 更新系統帳號操作範圍（整批取代）
    /// </summary>
    public sealed class UpdateSystemAccountScopesRequest
    {
        public List<string> Scopes { get; set; } = [];
    }

    /// <summary>
    /// 變更系統帳號狀態（停用 / 凍結需填寫原因）
    /// </summary>
    public sealed class ChangeSystemAccountStatusRequest
    {
        public SystemAccountStatus Status { get; set; }

        [StringLength(200)]
        public string? Reason { get; set; }
    }

    /// <summary>
    /// 輪替金鑰
    /// </summary>
    public sealed class RotateSystemAccountSecretRequest
    {
        /// <summary>是否同時撤銷以舊金鑰換得、尚未過期的權杖（疑似外洩時應勾選）</summary>
        public bool RevokeExistingTokens { get; set; } = true;

        /// <summary>新金鑰有效天數（null 表示不到期）</summary>
        [Range(1, 3650)]
        public int? SecretValidDays { get; set; } = 365;

        [StringLength(200)]
        public string? Reason { get; set; }
    }

    /// <summary>
    /// 撤銷系統帳號所有已簽發權杖
    /// </summary>
    public sealed class RevokeSystemAccountTokensRequest
    {
        [StringLength(200)]
        public string? Reason { get; set; }
    }

    /// <summary>
    /// 建立帳號 / 輪替金鑰結果：client_secret 明文僅於此回應出現一次，伺服器不保存
    /// </summary>
    public sealed class SystemAccountSecretResultDto
    {
        public SystemAccountDto Account { get; init; } = new();
        public string ClientSecret { get; init; } = string.Empty;
    }

    /// <summary>
    /// Scope 目錄項目（由程式中標註的 [RequiresSystemScope] 自動彙整）
    /// </summary>
    public sealed class SystemScopeCatalogItemDto
    {
        public string Scope { get; init; } = string.Empty;
        public string? Description { get; init; }

        /// <summary>需要此 Scope 的端點（HTTP 方法 + 路由）</summary>
        public IReadOnlyList<string> Endpoints { get; init; } = [];
    }

    /// <summary>
    /// 系統帳號稽核歷程
    /// </summary>
    public sealed class SystemAccountAuditLogDto
    {
        public Guid Id { get; init; }
        public string SystemAccountId { get; init; } = string.Empty;
        public string Action { get; init; } = string.Empty;
        public string Summary { get; init; } = string.Empty;
        public string? ActorName { get; init; }
        public string? ClientIp { get; init; }
        public string? CorrelationId { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
    }

    /// <summary>
    /// OAuth 2.0 Client Credentials 權杖請求（支援 application/x-www-form-urlencoded 與 JSON）
    /// </summary>
    public sealed class SystemTokenRequest
    {
        [JsonPropertyName("grant_type")]
        public string? GrantType { get; set; }

        [JsonPropertyName("client_id")]
        public string? ClientId { get; set; }

        [JsonPropertyName("client_secret")]
        public string? ClientSecret { get; set; }

        /// <summary>以空白分隔的 Scope；省略時核發帳號被授予的全部 Scope</summary>
        [JsonPropertyName("scope")]
        public string? Scope { get; set; }
    }

    /// <summary>
    /// OAuth 2.0 權杖回應（RFC 6749 §5.1）
    /// </summary>
    public sealed class SystemTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;

        [JsonPropertyName("token_type")]
        public string TokenType { get; init; } = "Bearer";

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }

        [JsonPropertyName("scope")]
        public string Scope { get; init; } = string.Empty;
    }

    /// <summary>
    /// 換發權杖結果；失敗時 Error 為 OAuth 錯誤碼（invalid_request / invalid_client / invalid_scope / unsupported_grant_type）
    /// </summary>
    public sealed record SystemTokenIssueResult(SystemTokenResponse? Token, string? Error, string? ErrorDescription)
    {
        public bool Succeeded => Token is not null;

        public static SystemTokenIssueResult Success(SystemTokenResponse token) => new(token, null, null);

        public static SystemTokenIssueResult Failure(string error, string description) => new(null, error, description);
    }

    /// <summary>
    /// 系統帳號權杖自我檢查結果（供服務部署後驗證連線與 Scope）
    /// </summary>
    public sealed class SystemAccountWhoAmIDto
    {
        public string AccountId { get; init; } = string.Empty;
        public string? ClientId { get; init; }
        public string? DisplayName { get; init; }
        public IReadOnlyList<string> Scopes { get; init; } = [];
        public DateTimeOffset? ExpiresAt { get; init; }
        public string? CorrelationId { get; init; }
    }
}
