using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.Core.Abstractions.Entities.SystemAccounts;
using SGSFramework.Core.Abstractions.Identities;
using SGSFramework.Core.Tracing;
using SGSFramework.Identity.Abstractions;
using SGSFramework.Identity.DTOs;
using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace SGSFramework.Identity.Services
{
    /// <summary>
    /// 系統帳號（M2M）管理與權杖換發服務實作：
    /// 帳號持久化於 core.SystemAccounts / core.SystemAccountScopes，所有異動與權杖換發結果寫入 core.SystemAccountAuditLogs。
    /// 金鑰僅保存 PBKDF2 雜湊（ASP.NET Core Identity PasswordHasher V3），明文只在建立 / 輪替時回傳一次。
    /// </summary>
    public sealed partial class SystemAccountService(
        DbContext dbContext,
        IMemoryCache cache,
        ICurrentUserService currentUserService,
        ITokenManager tokenManager,
        TimeProvider timeProvider,
        ILogger<SystemAccountService> logger) : ISystemAccountService
    {
        public const int MaxScopes = 50;
        public const int MinTokenLifetimeMinutes = 5;
        public const int MaxTokenLifetimeMinutes = 60;
        private const string SecretPrefix = "sgs_sk_";

        private static readonly PasswordHasher<SystemAccount> SecretHasher = new();

        // 用戶端不存在時仍執行一次雜湊比對，避免以回應時間差探測有效的 client_id
        private static readonly Lazy<string> DummySecretHash = new(() => SecretHasher.HashPassword(new SystemAccount(), GenerateSecret()));

        private readonly DbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        private readonly IMemoryCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        private readonly ICurrentUserService _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        private readonly ITokenManager _tokenManager = tokenManager ?? throw new ArgumentNullException(nameof(tokenManager));
        private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        private readonly ILogger<SystemAccountService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        [GeneratedRegex("^[a-z][a-z0-9_]{2,45}$", RegexOptions.CultureInvariant)]
        private static partial Regex CodeRegex();

        [GeneratedRegex("^[a-z][a-z0-9-]{2,99}$", RegexOptions.CultureInvariant)]
        private static partial Regex ClientIdRegex();

        [GeneratedRegex(@"^[a-z][a-z0-9_-]*(\.[a-z0-9_-]+)+$", RegexOptions.CultureInvariant)]
        private static partial Regex ScopeRegex();

        private DbSet<SystemAccount> Accounts => _dbContext.Set<SystemAccount>();

        // ===== 查詢 =====

        public async Task<IReadOnlyList<SystemAccountDto>> GetAccountsAsync(CancellationToken cancellationToken = default)
        {
            var accounts = await Accounts
                .AsNoTracking()
                .Include(a => a.Scopes)
                .OrderBy(a => a.Id)
                .ToListAsync(cancellationToken);

            return accounts.Select(ToDto).ToList();
        }

        public async Task<SystemAccountDto?> GetAccountAsync(string id, CancellationToken cancellationToken = default)
        {
            var account = await Accounts
                .AsNoTracking()
                .Include(a => a.Scopes)
                .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

            return account is null ? null : ToDto(account);
        }

        public async Task<IReadOnlyList<SystemAccountAuditLogDto>> GetAuditLogsAsync(string? accountId, int take, CancellationToken cancellationToken = default)
        {
            take = Math.Clamp(take, 1, 500);

            var query = _dbContext.Set<SystemAccountAuditLog>().AsNoTracking();
            if (!string.IsNullOrWhiteSpace(accountId))
            {
                query = query.Where(l => l.SystemAccountId == accountId);
            }

            return await query
                .OrderByDescending(l => l.CreatedAt)
                .Take(take)
                .Select(l => new SystemAccountAuditLogDto
                {
                    Id = l.Id,
                    SystemAccountId = l.SystemAccountId,
                    Action = l.Action,
                    Summary = l.Summary,
                    ActorName = l.ActorName,
                    ClientIp = l.ClientIp,
                    CorrelationId = l.CorrelationId,
                    CreatedAt = l.CreatedAt
                })
                .ToListAsync(cancellationToken);
        }

        // ===== 管理 =====

        public async Task<SystemAccountSecretResultDto> CreateAsync(CreateSystemAccountRequest request, string? clientIp, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var code = request.Code?.Trim().ToLowerInvariant() ?? string.Empty;
            if (code.StartsWith(SystemAccountClaims.IdPrefix, StringComparison.Ordinal))
            {
                code = code[SystemAccountClaims.IdPrefix.Length..];
            }
            if (!CodeRegex().IsMatch(code))
            {
                throw new ArgumentException("帳號代碼須以小寫英文字母開頭，僅能包含小寫英數與底線，長度 3 到 46 字。");
            }

            var clientId = request.ClientId?.Trim().ToLowerInvariant() ?? string.Empty;
            if (!ClientIdRegex().IsMatch(clientId))
            {
                throw new ArgumentException("用戶端識別碼須以小寫英文字母開頭，僅能包含小寫英數與連字號，長度 3 到 100 字。");
            }

            var id = SystemAccountClaims.IdPrefix + code;
            if (await Accounts.AnyAsync(a => a.Id == id, cancellationToken))
            {
                throw new ArgumentException($"帳號代碼 {id} 已存在。");
            }
            if (await Accounts.AnyAsync(a => a.ClientId == clientId, cancellationToken))
            {
                throw new ArgumentException($"用戶端識別碼 {clientId} 已被其他系統帳號使用，嚴禁多個服務共用同一組身分。");
            }

            var name = NormalizeName(request.Name);
            var description = NormalizeDescription(request.Description);
            var lifetime = NormalizeLifetime(request.AccessTokenLifetimeMinutes);
            var ipRanges = NormalizeIpRanges(request.AllowedIpRanges);
            var scopes = NormalizeScopes(request.Scopes);
            var now = _timeProvider.GetUtcNow();
            var secret = GenerateSecret();

            var account = new SystemAccount
            {
                Id = id,
                ClientId = clientId,
                Name = name,
                Description = description,
                Status = SystemAccountStatus.Active,
                AccessTokenLifetimeMinutes = lifetime,
                AllowedIpRanges = ipRanges,
                SecretRotatedAt = now,
                SecretExpiresAt = ExpiryFrom(now, request.SecretValidDays),
                TokensNotBefore = now,
                CreatedAt = now,
                CreatedByName = _currentUserService.UserName
            };
            SetSecret(account, secret);
            foreach (var scope in scopes)
            {
                account.Scopes.Add(new SystemAccountScope { SystemAccountId = id, Scope = scope, GrantedAt = now });
            }

            Accounts.Add(account);
            AddAuditLog(id, SystemAccountAuditActions.Created,
                $"建立系統帳號「{name}」（client_id：{clientId}）；操作範圍：{FormatScopes(scopes)}；權杖有效 {lifetime} 分鐘；金鑰到期：{FormatDate(account.SecretExpiresAt)}",
                clientIp, now);

            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("已建立系統帳號 {AccountId}（client_id：{ClientId}），操作者：{Actor}", id, clientId, _currentUserService.UserName);

            return new SystemAccountSecretResultDto { Account = ToDto(account), ClientSecret = secret };
        }

        public async Task<SystemAccountDto> UpdateAsync(string id, UpdateSystemAccountRequest request, string? clientIp, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var account = await FindTrackedAsync(id, cancellationToken);
            EnsureNotFrozen(account);

            var name = NormalizeName(request.Name);
            var description = NormalizeDescription(request.Description);
            var lifetime = NormalizeLifetime(request.AccessTokenLifetimeMinutes);
            var ipRanges = NormalizeIpRanges(request.AllowedIpRanges);

            var changes = new List<string>();
            if (!string.Equals(account.Name, name, StringComparison.Ordinal)) changes.Add($"顯示名稱：{account.Name} → {name}");
            if (!string.Equals(account.Description, description, StringComparison.Ordinal)) changes.Add("說明：已更新");
            if (account.AccessTokenLifetimeMinutes != lifetime) changes.Add($"權杖有效分鐘數：{account.AccessTokenLifetimeMinutes} → {lifetime}");
            if (!string.Equals(account.AllowedIpRanges, ipRanges, StringComparison.Ordinal))
            {
                changes.Add($"允許來源 IP：{account.AllowedIpRanges ?? "不限制"} → {ipRanges ?? "不限制"}");
            }

            if (changes.Count == 0)
            {
                return ToDto(account);
            }

            var now = _timeProvider.GetUtcNow();
            account.Name = name;
            account.Description = description;
            account.AccessTokenLifetimeMinutes = lifetime;
            account.AllowedIpRanges = ipRanges;
            Touch(account, now);

            AddAuditLog(id, SystemAccountAuditActions.Updated, string.Join("；", changes), clientIp, now);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return ToDto(account);
        }

        public async Task<SystemAccountDto> UpdateScopesAsync(string id, UpdateSystemAccountScopesRequest request, string? clientIp, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var account = await FindTrackedAsync(id, cancellationToken);
            EnsureNotFrozen(account);

            var scopes = NormalizeScopes(request.Scopes);
            var current = account.Scopes.Select(s => s.Scope).ToHashSet(StringComparer.Ordinal);
            var added = scopes.Where(s => !current.Contains(s)).ToList();
            var removed = current.Where(s => !scopes.Contains(s)).OrderBy(s => s, StringComparer.Ordinal).ToList();

            if (added.Count == 0 && removed.Count == 0)
            {
                return ToDto(account);
            }

            var now = _timeProvider.GetUtcNow();
            foreach (var scope in account.Scopes.Where(s => removed.Contains(s.Scope)).ToList())
            {
                account.Scopes.Remove(scope);
            }
            foreach (var scope in added)
            {
                account.Scopes.Add(new SystemAccountScope { SystemAccountId = id, Scope = scope, GrantedAt = now });
            }

            // 權杖內含 Scope，異動後撤銷既有權杖，用戶端收到 401 會自動以新 Scope 重新換發
            account.TokensNotBefore = now;
            Touch(account, now);

            var summary = new List<string>();
            if (added.Count > 0) summary.Add($"新增：{string.Join("、", added)}");
            if (removed.Count > 0) summary.Add($"移除：{string.Join("、", removed)}");
            summary.Add("既有權杖已撤銷");

            AddAuditLog(id, SystemAccountAuditActions.ScopesChanged, string.Join("；", summary), clientIp, now);
            await _dbContext.SaveChangesAsync(cancellationToken);
            InvalidateCache(id);

            return ToDto(account);
        }

        public async Task<SystemAccountDto> ChangeStatusAsync(string id, ChangeSystemAccountStatusRequest request, string? clientIp, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (!Enum.IsDefined(request.Status))
            {
                throw new ArgumentException("無效的帳號狀態。");
            }

            var account = await FindTrackedAsync(id, cancellationToken);
            if (account.Status == request.Status)
            {
                return ToDto(account);
            }

            var reason = request.Reason?.Trim();
            var requiresReason = request.Status != SystemAccountStatus.Active || account.Status == SystemAccountStatus.Frozen;
            if (requiresReason && string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException(account.Status == SystemAccountStatus.Frozen ? "解除凍結必須填寫原因。" : "停用或凍結帳號必須填寫原因。");
            }

            if (account.Status == SystemAccountStatus.Frozen && request.Status == SystemAccountStatus.Disabled)
            {
                throw new InvalidOperationException("凍結中的帳號須先解除凍結，才能改為停用。");
            }

            var now = _timeProvider.GetUtcNow();
            var previous = account.Status;
            account.Status = request.Status;

            // 停用 / 凍結時撤銷既有權杖，避免日後重新啟用時舊權杖復活
            if (request.Status != SystemAccountStatus.Active)
            {
                account.TokensNotBefore = now;
            }
            Touch(account, now);

            var summary = $"狀態：{StatusLabel(previous)} → {StatusLabel(request.Status)}";
            if (!string.IsNullOrWhiteSpace(reason)) summary += $"；原因：{reason}";
            if (request.Status != SystemAccountStatus.Active) summary += "；既有權杖已撤銷";

            AddAuditLog(id, SystemAccountAuditActions.StatusChanged, summary, clientIp, now);
            await _dbContext.SaveChangesAsync(cancellationToken);
            InvalidateCache(id);

            _logger.LogWarning("系統帳號 {AccountId} 狀態變更：{Previous} → {Current}，操作者：{Actor}，原因：{Reason}",
                id, previous, request.Status, _currentUserService.UserName, reason);

            return ToDto(account);
        }

        public async Task<SystemAccountSecretResultDto> RotateSecretAsync(string id, RotateSystemAccountSecretRequest request, string? clientIp, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var account = await FindTrackedAsync(id, cancellationToken);
            var now = _timeProvider.GetUtcNow();
            var secret = GenerateSecret();

            SetSecret(account, secret);
            account.SecretRotatedAt = now;
            account.SecretExpiresAt = ExpiryFrom(now, request.SecretValidDays);
            if (request.RevokeExistingTokens)
            {
                account.TokensNotBefore = now;
            }
            Touch(account, now);

            var summary = $"金鑰已輪替（新金鑰 {account.SecretHint}，到期：{FormatDate(account.SecretExpiresAt)}）；"
                          + (request.RevokeExistingTokens ? "既有權杖已撤銷" : "既有權杖於到期前仍有效");
            if (!string.IsNullOrWhiteSpace(request.Reason)) summary += $"；原因：{request.Reason.Trim()}";

            AddAuditLog(id, SystemAccountAuditActions.SecretRotated, summary, clientIp, now);
            await _dbContext.SaveChangesAsync(cancellationToken);
            InvalidateCache(id);

            _logger.LogWarning("系統帳號 {AccountId} 金鑰已輪替，撤銷既有權杖：{Revoke}，操作者：{Actor}", id, request.RevokeExistingTokens, _currentUserService.UserName);

            return new SystemAccountSecretResultDto { Account = ToDto(account), ClientSecret = secret };
        }

        public async Task<SystemAccountDto> RevokeTokensAsync(string id, RevokeSystemAccountTokensRequest request, string? clientIp, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var account = await FindTrackedAsync(id, cancellationToken);
            var now = _timeProvider.GetUtcNow();

            account.TokensNotBefore = now;
            Touch(account, now);

            var summary = "已撤銷所有已簽發權杖";
            if (!string.IsNullOrWhiteSpace(request.Reason)) summary += $"；原因：{request.Reason.Trim()}";

            AddAuditLog(id, SystemAccountAuditActions.TokensRevoked, summary, clientIp, now);
            await _dbContext.SaveChangesAsync(cancellationToken);
            InvalidateCache(id);

            return ToDto(account);
        }

        public async Task DeleteAsync(string id, string? clientIp, CancellationToken cancellationToken = default)
        {
            var account = await FindTrackedAsync(id, cancellationToken);
            if (account.Status != SystemAccountStatus.Disabled)
            {
                throw new InvalidOperationException("僅能刪除已停用的系統帳號，請先停用並確認沒有服務仍在使用。");
            }

            var now = _timeProvider.GetUtcNow();
            Accounts.Remove(account);
            AddAuditLog(id, SystemAccountAuditActions.Deleted,
                $"刪除系統帳號「{account.Name}」（client_id：{account.ClientId}）；原操作範圍：{FormatScopes(account.Scopes.Select(s => s.Scope))}",
                clientIp, now);

            await _dbContext.SaveChangesAsync(cancellationToken);
            InvalidateCache(id);

            _logger.LogWarning("已刪除系統帳號 {AccountId}，操作者：{Actor}", id, _currentUserService.UserName);
        }

        // ===== 權杖換發 =====

        public async Task<SystemTokenIssueResult> IssueTokenAsync(SystemTokenRequest request, string? clientIp, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (!string.Equals(request.GrantType, "client_credentials", StringComparison.Ordinal))
            {
                return SystemTokenIssueResult.Failure("unsupported_grant_type", "僅支援 grant_type=client_credentials。");
            }

            var clientId = request.ClientId?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(request.ClientSecret))
            {
                return SystemTokenIssueResult.Failure("invalid_request", "缺少 client_id 或 client_secret。");
            }

            var account = await Accounts
                .Include(a => a.Scopes)
                .FirstOrDefaultAsync(a => a.ClientId == clientId, cancellationToken);

            if (account is null)
            {
                SecretHasher.VerifyHashedPassword(new SystemAccount(), DummySecretHash.Value, request.ClientSecret);
                _logger.LogWarning("[SystemToken] 未知的 client_id 嘗試換發權杖：{ClientId}，來源 IP：{ClientIp}", clientId, clientIp);
                return InvalidClient();
            }

            var now = _timeProvider.GetUtcNow();
            var verification = SecretHasher.VerifyHashedPassword(account, account.SecretHash, request.ClientSecret);

            // 回應一律為 invalid_client，不向呼叫端揭露失敗細節；詳細原因寫入稽核歷程
            string? denial = verification == PasswordVerificationResult.Failed
                ? "金鑰錯誤"
                : account.Status != SystemAccountStatus.Active
                    ? $"帳號{StatusLabel(account.Status)}"
                    : account.SecretExpiresAt is { } expiresAt && expiresAt <= now
                        ? $"金鑰已於 {FormatDate(expiresAt)} 到期"
                        : !IsIpAllowed(account.AllowedIpRanges, clientIp)
                            ? $"來源 IP {clientIp ?? "未知"} 不在允許清單"
                            : null;

            if (denial is not null)
            {
                AddAuditLog(account.Id, SystemAccountAuditActions.TokenDenied, $"拒絕換發權杖：{denial}", clientIp, now, actorName: clientId);
                await _dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogWarning("[SystemToken] 拒絕系統帳號 {AccountId} 換發權杖：{Reason}，來源 IP：{ClientIp}", account.Id, denial, clientIp);
                return InvalidClient();
            }

            var granted = account.Scopes.Select(s => s.Scope).OrderBy(s => s, StringComparer.Ordinal).ToList();
            var requested = (request.Scope ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => s.ToLowerInvariant())
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var notGranted = requested.Where(s => !granted.Contains(s, StringComparer.Ordinal)).ToList();
            if (notGranted.Count > 0)
            {
                AddAuditLog(account.Id, SystemAccountAuditActions.TokenDenied, $"拒絕換發權杖：要求未授予的操作範圍 {string.Join("、", notGranted)}", clientIp, now, actorName: clientId);
                await _dbContext.SaveChangesAsync(cancellationToken);
                return SystemTokenIssueResult.Failure("invalid_scope", $"未被授予的操作範圍：{string.Join(" ", notGranted)}");
            }

            var tokenScopes = requested.Count > 0 ? requested : granted;
            var token = _tokenManager.GenerateSystemAccessToken(new SystemAccessTokenRequest(
                account.Id,
                account.ClientId,
                account.Name,
                tokenScopes,
                TimeSpan.FromMinutes(NormalizeLifetime(account.AccessTokenLifetimeMinutes))));

            if (verification == PasswordVerificationResult.SuccessRehashNeeded)
            {
                account.SecretHash = SecretHasher.HashPassword(account, request.ClientSecret);
            }
            account.LastUsedAt = now;
            account.LastUsedIp = clientIp;

            AddAuditLog(account.Id, SystemAccountAuditActions.TokenIssued,
                $"換發權杖，操作範圍：{FormatScopes(tokenScopes)}，到期：{token.ExpiresAt.ToLocalTime():yyyy/MM/dd HH:mm:ss}",
                clientIp, now, actorName: clientId);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return SystemTokenIssueResult.Success(new SystemTokenResponse
            {
                AccessToken = token.AccessToken,
                TokenType = "Bearer",
                ExpiresIn = (int)Math.Round((token.ExpiresAt - token.IssuedAt).TotalSeconds),
                Scope = string.Join(' ', tokenScopes)
            });
        }

        private static SystemTokenIssueResult InvalidClient() =>
            SystemTokenIssueResult.Failure("invalid_client", "用戶端驗證失敗。");

        // ===== 內部輔助 =====

        private async Task<SystemAccount> FindTrackedAsync(string id, CancellationToken cancellationToken) =>
            await Accounts.Include(a => a.Scopes).FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"找不到系統帳號 {id}。");

        private static void EnsureNotFrozen(SystemAccount account)
        {
            if (account.Status == SystemAccountStatus.Frozen)
            {
                throw new InvalidOperationException("帳號凍結中，請先解除凍結再修改設定。");
            }
        }

        private void Touch(SystemAccount account, DateTimeOffset now)
        {
            account.UpdatedAt = now;
            account.UpdatedByName = _currentUserService.UserName;
        }

        private void AddAuditLog(string accountId, string action, string summary, string? clientIp, DateTimeOffset now, string? actorName = null)
        {
            var isUserAction = actorName is null;
            _dbContext.Set<SystemAccountAuditLog>().Add(new SystemAccountAuditLog
            {
                SystemAccountId = accountId,
                Action = action,
                Summary = summary.Length > 2000 ? summary[..2000] : summary,
                ActorUserId = isUserAction && _currentUserService.UserGuid != Guid.Empty ? _currentUserService.UserGuid : null,
                ActorName = isUserAction ? _currentUserService.UserName : actorName,
                ClientIp = clientIp,
                CorrelationId = CorrelationContext.Current,
                CreatedAt = now
            });
        }

        private void InvalidateCache(string id) => _cache.Remove(SystemAccountTokenValidator.CacheKey(id));

        private static void SetSecret(SystemAccount account, string secret)
        {
            account.SecretHash = SecretHasher.HashPassword(account, secret);
            account.SecretHint = "…" + secret[^4..];
        }

        /// <summary>產生 256 位元隨機金鑰（Base64Url），加上前綴便於秘密掃描工具辨識外洩</summary>
        private static string GenerateSecret() =>
            SecretPrefix + Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

        private static DateTimeOffset? ExpiryFrom(DateTimeOffset now, int? validDays) =>
            validDays is > 0 ? now.AddDays(Math.Min(validDays.Value, 3650)) : null;

        private static string NormalizeName(string? name)
        {
            var normalized = name?.Trim() ?? string.Empty;
            if (normalized.Length is 0 or > 100)
            {
                throw new ArgumentException("顯示名稱不可為空，且長度不可超過 100 字。");
            }
            return normalized;
        }

        private static string? NormalizeDescription(string? description)
        {
            var normalized = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
            if (normalized?.Length > 500)
            {
                throw new ArgumentException("說明長度不可超過 500 字。");
            }
            return normalized;
        }

        private static int NormalizeLifetime(int minutes)
        {
            if (minutes is < MinTokenLifetimeMinutes or > MaxTokenLifetimeMinutes)
            {
                throw new ArgumentException($"權杖有效時間必須介於 {MinTokenLifetimeMinutes} 到 {MaxTokenLifetimeMinutes} 分鐘之間。");
            }
            return minutes;
        }

        private static List<string> NormalizeScopes(IEnumerable<string>? scopes)
        {
            var normalized = (scopes ?? [])
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim().ToLowerInvariant())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToList();

            if (normalized.Count > MaxScopes)
            {
                throw new ArgumentException($"操作範圍不可超過 {MaxScopes} 個。");
            }

            var invalid = normalized.Where(s => s.Length > 100 || !ScopeRegex().IsMatch(s)).ToList();
            if (invalid.Count > 0)
            {
                throw new ArgumentException($"操作範圍格式不正確（應為「資源.動作」小寫格式，例如 inventory.write）：{string.Join("、", invalid)}");
            }

            return normalized;
        }

        /// <summary>驗證並正規化 IP / CIDR 清單（逗號、分號或換行分隔），空白表示不限制</summary>
        private static string? NormalizeIpRanges(string? ranges)
        {
            if (string.IsNullOrWhiteSpace(ranges))
            {
                return null;
            }

            var items = ranges
                .Split([',', ';', '\n', '\r', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var invalid = items.Where(i => !TryParseRange(i, out _)).ToList();
            if (invalid.Count > 0)
            {
                throw new ArgumentException($"允許來源 IP 格式不正確（支援單一 IP 或 CIDR，例如 10.0.0.0/8）：{string.Join("、", invalid)}");
            }

            var normalized = string.Join(",", items);
            if (normalized.Length > 500)
            {
                throw new ArgumentException("允許來源 IP 清單過長（上限 500 字）。");
            }
            return normalized;
        }

        private static bool IsIpAllowed(string? allowedRanges, string? clientIp)
        {
            if (string.IsNullOrWhiteSpace(allowedRanges))
            {
                return true;
            }

            if (!IPAddress.TryParse(clientIp, out var address))
            {
                return false;
            }
            if (address.IsIPv4MappedToIPv6)
            {
                address = address.MapToIPv4();
            }

            return allowedRanges
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(range => TryParseRange(range, out var network) && network.Contains(address));
        }

        private static bool TryParseRange(string value, out IPNetwork network)
        {
            if (value.Contains('/'))
            {
                return IPNetwork.TryParse(value, out network);
            }

            if (IPAddress.TryParse(value, out var single))
            {
                network = new IPNetwork(single, single.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128);
                return true;
            }

            network = default;
            return false;
        }

        private static string StatusLabel(SystemAccountStatus status) => status switch
        {
            SystemAccountStatus.Active => "啟用",
            SystemAccountStatus.Disabled => "停用",
            SystemAccountStatus.Frozen => "凍結",
            _ => status.ToString()
        };

        private static string FormatScopes(IEnumerable<string> scopes)
        {
            var list = scopes.ToList();
            return list.Count == 0 ? "（無）" : string.Join("、", list);
        }

        private static string FormatDate(DateTimeOffset? value) =>
            value is { } v ? v.ToLocalTime().ToString("yyyy/MM/dd") : "不到期";

        private static SystemAccountDto ToDto(SystemAccount account) => new()
        {
            Id = account.Id,
            ClientId = account.ClientId,
            Name = account.Name,
            Description = account.Description,
            Status = account.Status,
            Scopes = account.Scopes.Select(s => s.Scope).OrderBy(s => s, StringComparer.Ordinal).ToList(),
            AccessTokenLifetimeMinutes = account.AccessTokenLifetimeMinutes,
            AllowedIpRanges = account.AllowedIpRanges,
            SecretHint = account.SecretHint,
            SecretExpiresAt = account.SecretExpiresAt,
            SecretRotatedAt = account.SecretRotatedAt,
            TokensNotBefore = account.TokensNotBefore,
            LastUsedAt = account.LastUsedAt,
            LastUsedIp = account.LastUsedIp,
            CreatedAt = account.CreatedAt,
            CreatedBy = account.CreatedByName,
            UpdatedAt = account.UpdatedAt,
            UpdatedBy = account.UpdatedByName
        };
    }
}
