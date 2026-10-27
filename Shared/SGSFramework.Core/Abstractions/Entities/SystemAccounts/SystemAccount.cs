using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace SGSFramework.Core.Abstractions.Entities.SystemAccounts
{
    /// <summary>
    /// 系統帳號狀態
    /// </summary>
    public enum SystemAccountStatus
    {
        /// <summary>停用：不可換發權杖，已簽發權杖立即失效</summary>
        Disabled = 0,

        /// <summary>啟用</summary>
        Active = 1,

        /// <summary>凍結：疑似金鑰外洩等資安事件時使用，效果同停用，但僅能以「解除凍結」恢復</summary>
        Frozen = 2
    }

    /// <summary>
    /// 系統帳號（服務間通訊 M2M 身分，例如排程服務、內部微服務）。
    /// 不使用密碼 / OTP，而以 OAuth 2.0 Client Credentials（client_id + client_secret）換取短效 JWT；
    /// 不綁定角色，而是綁定操作範圍（Scope）。人類帳號仍由 AspNetUsers 管理，兩者以權杖中的 account_type 區隔。
    /// </summary>
    [DisplayName("系統帳號")]
    [Comment("系統帳號（服務間通訊 M2M 身分），以 Client Credentials 換取短效 JWT，並以 Scope 授權")]
    public class SystemAccount
    {
        [DisplayName("識別主鍵")]
        [Comment("識別主鍵（含 sys_ 前綴，例如 sys_hangfire_worker）")]
        public string Id { get; set; } = string.Empty;

        [DisplayName("用戶端識別碼")]
        [Comment("用戶端識別碼 client_id（例如 hangfire-job-runner）")]
        public string ClientId { get; set; } = string.Empty;

        [DisplayName("金鑰雜湊值")]
        [Comment("client_secret 雜湊值（PBKDF2，不儲存明文）")]
        public string SecretHash { get; set; } = string.Empty;

        [DisplayName("金鑰提示")]
        [Comment("client_secret 末四碼，供維運人員比對部署中的金鑰版本")]
        public string? SecretHint { get; set; }

        [DisplayName("顯示名稱")]
        [Comment("顯示名稱")]
        public string Name { get; set; } = string.Empty;

        [DisplayName("說明")]
        [Comment("用途說明（使用的服務、負責人等）")]
        public string? Description { get; set; }

        [DisplayName("狀態")]
        [Comment("狀態：0 停用、1 啟用、2 凍結")]
        public SystemAccountStatus Status { get; set; } = SystemAccountStatus.Active;

        [DisplayName("權杖有效分鐘數")]
        [Comment("簽發之 Access Token 有效分鐘數")]
        public int AccessTokenLifetimeMinutes { get; set; } = 15;

        [DisplayName("允許來源 IP")]
        [Comment("允許換發權杖的來源 IP / CIDR（逗號分隔，空白表示不限制）")]
        public string? AllowedIpRanges { get; set; }

        [DisplayName("金鑰到期時間")]
        [Comment("client_secret 到期時間（空白表示不到期），到期後須輪替金鑰才能換發權杖")]
        public DateTimeOffset? SecretExpiresAt { get; set; }

        [DisplayName("金鑰輪替時間")]
        [Comment("client_secret 最近一次產生 / 輪替時間")]
        public DateTimeOffset SecretRotatedAt { get; set; }

        [DisplayName("權杖生效起點")]
        [Comment("早於此時間簽發的權杖一律視為無效（撤銷既有權杖時更新）")]
        public DateTimeOffset TokensNotBefore { get; set; }

        [DisplayName("最近使用時間")]
        [Comment("最近一次成功換發權杖時間")]
        public DateTimeOffset? LastUsedAt { get; set; }

        [DisplayName("最近使用 IP")]
        [Comment("最近一次成功換發權杖的來源 IP")]
        public string? LastUsedIp { get; set; }

        [DisplayName("建立時間")]
        [Comment("建立時間")]
        public DateTimeOffset CreatedAt { get; set; }

        [DisplayName("建立者名稱")]
        [Comment("建立者名稱")]
        public string? CreatedByName { get; set; }

        [DisplayName("更新時間")]
        [Comment("更新時間")]
        public DateTimeOffset? UpdatedAt { get; set; }

        [DisplayName("更新者名稱")]
        [Comment("更新者名稱")]
        public string? UpdatedByName { get; set; }

        public ICollection<SystemAccountScope> Scopes { get; set; } = new List<SystemAccountScope>();
    }

    /// <summary>
    /// 系統帳號被授予的操作範圍（Scope），例如 inventory.write、order.read
    /// </summary>
    [DisplayName("系統帳號操作範圍")]
    [Comment("系統帳號被授予的操作範圍（Scope）")]
    public class SystemAccountScope
    {
        [DisplayName("系統帳號識別鍵")]
        [Comment("系統帳號識別鍵")]
        public string SystemAccountId { get; set; } = string.Empty;

        [DisplayName("操作範圍")]
        [Comment("操作範圍（例如 inventory.write）")]
        public string Scope { get; set; } = string.Empty;

        [DisplayName("授予時間")]
        [Comment("授予時間")]
        public DateTimeOffset GrantedAt { get; set; }
    }

    /// <summary>
    /// 系統帳號稽核歷程（建立、異動、金鑰輪替、狀態變更、權杖換發 / 拒絕），僅新增不修改，刪除帳號後仍保留。
    /// </summary>
    [DisplayName("系統帳號稽核歷程")]
    [Comment("系統帳號稽核歷程（僅新增不修改，刪除帳號後仍保留）")]
    public class SystemAccountAuditLog
    {
        [DisplayName("識別主鍵")]
        [Comment("識別主鍵")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [DisplayName("系統帳號識別鍵")]
        [Comment("系統帳號識別鍵")]
        public string SystemAccountId { get; set; } = string.Empty;

        [DisplayName("動作")]
        [Comment("動作（Created、Updated、ScopesChanged、StatusChanged、SecretRotated、Deleted、TokenIssued、TokenDenied）")]
        public string Action { get; set; } = string.Empty;

        [DisplayName("摘要")]
        [Comment("可讀的異動摘要")]
        public string Summary { get; set; } = string.Empty;

        [DisplayName("操作者用戶識別鍵")]
        [Comment("操作者用戶識別鍵（權杖換發時為空）")]
        public Guid? ActorUserId { get; set; }

        [DisplayName("操作者名稱")]
        [Comment("操作者名稱（權杖換發時為 client_id）")]
        public string? ActorName { get; set; }

        [DisplayName("客戶端 IP")]
        [Comment("客戶端 IP")]
        public string? ClientIp { get; set; }

        [DisplayName("追蹤碼")]
        [Comment("請求追蹤碼（X-Correlation-ID）")]
        public string? CorrelationId { get; set; }

        [DisplayName("建立時間")]
        [Comment("建立時間")]
        public DateTimeOffset CreatedAt { get; set; }
    }

    /// <summary>
    /// 系統帳號稽核動作代碼
    /// </summary>
    public static class SystemAccountAuditActions
    {
        public const string Created = "Created";
        public const string Updated = "Updated";
        public const string ScopesChanged = "ScopesChanged";
        public const string StatusChanged = "StatusChanged";
        public const string SecretRotated = "SecretRotated";
        public const string TokensRevoked = "TokensRevoked";
        public const string Deleted = "Deleted";
        public const string TokenIssued = "TokenIssued";
        public const string TokenDenied = "TokenDenied";
    }

    public class SystemAccountConfiguration : IEntityTypeConfiguration<SystemAccount>
    {
        public void Configure(EntityTypeBuilder<SystemAccount> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.ToTable("SystemAccounts", "core");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasMaxLength(50);
            builder.Property(x => x.ClientId).HasMaxLength(100).IsRequired();
            builder.HasIndex(x => x.ClientId).IsUnique();
            builder.Property(x => x.SecretHash).HasMaxLength(255).IsRequired();
            builder.Property(x => x.SecretHint).HasMaxLength(16);
            builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
            builder.Property(x => x.Description).HasMaxLength(500);
            builder.Property(x => x.Status).HasConversion<int>();
            builder.Property(x => x.AllowedIpRanges).HasMaxLength(500);
            builder.Property(x => x.LastUsedIp).HasMaxLength(64);
            builder.Property(x => x.CreatedByName).HasMaxLength(256);
            builder.Property(x => x.UpdatedByName).HasMaxLength(256);

            builder.HasMany(x => x.Scopes)
                .WithOne()
                .HasForeignKey(x => x.SystemAccountId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class SystemAccountScopeConfiguration : IEntityTypeConfiguration<SystemAccountScope>
    {
        public void Configure(EntityTypeBuilder<SystemAccountScope> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.ToTable("SystemAccountScopes", "core");
            builder.HasKey(x => new { x.SystemAccountId, x.Scope });
            builder.Property(x => x.SystemAccountId).HasMaxLength(50);
            builder.Property(x => x.Scope).HasMaxLength(100);
        }
    }

    public class SystemAccountAuditLogConfiguration : IEntityTypeConfiguration<SystemAccountAuditLog>
    {
        public void Configure(EntityTypeBuilder<SystemAccountAuditLog> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.ToTable("SystemAccountAuditLogs", "core");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.SystemAccountId).HasMaxLength(50).IsRequired();
            builder.Property(x => x.Action).HasMaxLength(32).IsRequired();
            builder.Property(x => x.Summary).HasMaxLength(2000).IsRequired();
            builder.Property(x => x.ActorName).HasMaxLength(256);
            builder.Property(x => x.ClientIp).HasMaxLength(64);
            builder.Property(x => x.CorrelationId).HasMaxLength(64);
            builder.HasIndex(x => new { x.SystemAccountId, x.CreatedAt });
            builder.HasIndex(x => x.CreatedAt);
        }
    }
}
