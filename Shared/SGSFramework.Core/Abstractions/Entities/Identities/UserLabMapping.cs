using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGSFramework.Core.Abstractions.Entities.AuditLogs;

namespace SGSFramework.Core.Abstractions.Entities.Identities;

/// <summary>
/// UserLabMapping 用於管理用戶與實驗室之間的關聯，包括用戶的主要實驗室和職位標題等信息。
/// </summary>
public class UserLabMapping : IAuditable
{
    public Guid UserId { get; private set; }
    public int LabId { get; private set; }
    public Guid TenantLabId { get; private set; }
    public bool IsPrimary { get; private set; }
    public string? JobTitle { get; private set; }
    public DateTime EffectiveDate { get; private set; }
    public DateTime? ExpiryDate { get; private set; }
    public bool IsActive { get; private set; }

    // --- IAuditable 實作 ---
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }

    public virtual ApplicationUser User { get; set; } = null!;

    private UserLabMapping() { }

    /// <summary>
    /// 通用建立處理工廠 (由 IsPrimary 參數決定升降級)
    /// </summary>
    public static UserLabMapping Create(
        Guid userId,
        int labId,
        Guid tenantLabId,
        bool isPrimary,
        string? jobTitle = null,
        DateTime? effectiveDate = null,
        DateTime? expiryDate = null,
        string? operatorId = null)
    {
        ValidateInputs(userId, labId, tenantLabId);

        var nowUtc = DateTimeOffset.UtcNow;
        return new UserLabMapping
        {
            UserId = userId,
            LabId = labId,
            TenantLabId = tenantLabId,
            IsPrimary = isPrimary,
            JobTitle = jobTitle?.Trim(),
            EffectiveDate = effectiveDate ?? nowUtc.UtcDateTime,
            ExpiryDate = expiryDate,
            IsActive = true,
            CreatedAtUtc = nowUtc,
            CreatedBy = operatorId
        };
    }

    /// <summary>
    /// 創建主要用戶實驗室關聯
    /// </summary>
    public static UserLabMapping CreatePrimary(
        Guid userId,
        int labId,
        Guid tenantLabId,
        string? jobTitle = null,
        string? operatorId = null)
    {
        return Create(userId, labId, tenantLabId, isPrimary: true, jobTitle: jobTitle, operatorId: operatorId);
    }

    /// <summary>
    /// 創建次要用戶實驗室關聯
    /// </summary>
    public static UserLabMapping CreateSecondary(
        Guid userId,
        int labId,
        Guid tenantLabId,
        string? jobTitle = null,
        string? operatorId = null)
    {
        return Create(userId, labId, tenantLabId, isPrimary: false, jobTitle: jobTitle, operatorId: operatorId);
    }

    /// <summary>
    /// 更新詳細屬性內容 (領域領域邏輯)
    /// </summary>
    public void UpdateDetails(
        Guid tenantLabId,
        bool isPrimary,
        string? jobTitle,
        DateTime effectiveDate,
        DateTime? expiryDate,
        string? operatorId = null)
    {
        if (tenantLabId == Guid.Empty) throw new ArgumentException("TenantLabId 不能為 Empty Guid。", nameof(tenantLabId));

        TenantLabId = tenantLabId;
        IsPrimary = isPrimary;
        JobTitle = jobTitle?.Trim();
        EffectiveDate = effectiveDate;
        ExpiryDate = expiryDate;
        IsActive = true;

        SetUpdated(operatorId);
    }

    public void DemoteToSecondary(string? operatorId = null)
    {
        IsPrimary = false;
        SetUpdated(operatorId);
    }

    public void PromoteToPrimary(string? operatorId = null)
    {
        IsPrimary = true;
        SetUpdated(operatorId);
    }

    public void Deactivate(string? operatorId = null)
    {
        IsActive = false;
        SetUpdated(operatorId);
    }

    private void SetUpdated(string? operatorId)
    {
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = operatorId;
    }

    private static void ValidateInputs(Guid userId, int labId, Guid tenantLabId)
    {
        if (userId == Guid.Empty) throw new ArgumentException("UserId 不能為 Empty Guid。", nameof(userId));
        if (labId <= 0) throw new ArgumentOutOfRangeException(nameof(labId), "LabId 必須大於 0。");
        if (tenantLabId == Guid.Empty) throw new ArgumentException("TenantLabId 不能為 Empty Guid。", nameof(tenantLabId));
    }

    public bool IsValidAt(DateTime referenceTime)
    {
        if (!IsActive) return false;
        if (referenceTime < EffectiveDate) return false;
        if (ExpiryDate.HasValue && referenceTime > ExpiryDate.Value) return false;
        return true;
    }
}

/// <summary>
/// UserLabMapping 實體的 EF Core Fluent API 映射組態 (核心與外掛解耦設計)
/// 支援 IAuditable 稽核屬性與 MSSQL 2025 唯一性過濾索引
/// </summary>
public class UserLabMappingConfiguration : IEntityTypeConfiguration<UserLabMapping>
{
    public void Configure(EntityTypeBuilder<UserLabMapping> builder)
    {
        // 1. 資料表與 Schema 定義
        builder.ToTable("UserLabMappings", "core");

        // 2. 複合主鍵設定 (UserId + LabId [int])
        builder.HasKey(x => new { x.UserId, x.LabId });

        // 3. 業務與狀態屬性設定
        builder.Property(x => x.UserId)
            .IsRequired();

        builder.Property(x => x.LabId)
            .IsRequired();

        builder.Property(x => x.TenantLabId)
            .IsRequired();

        builder.Property(x => x.IsPrimary)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.JobTitle)
            .HasMaxLength(50)
            .IsRequired(false);

        builder.Property(x => x.EffectiveDate)
            .HasColumnType("datetime2(7)")
            .IsRequired();

        builder.Property(x => x.ExpiryDate)
            .HasColumnType("datetime2(7)")
            .IsRequired(false);

        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        // 4. IAuditable 稽核欄位屬性設定 (帶時區之 DateTimeOffset 映射)
        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("datetimeoffset(7)")
            .HasDefaultValueSql("SYSDATETIMEOFFSET()")
            .IsRequired();

        builder.Property(x => x.CreatedBy)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnType("datetimeoffset(7)")
            .IsRequired(false);

        builder.Property(x => x.UpdatedBy)
            .HasMaxLength(100)
            .IsRequired(false);

        // 5. 外鍵關係定義 (僅映射 Core 內部的 ApplicationUser 實體)
        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_UserLabMappings_Users_UserId");

        // 6. 索引設計 (針對外掛 LabId 與 TenantLabId 進行資料庫層級檢索優化)

        // MSSQL 2025 唯一性過濾索引：強制單一使用者在啟用狀態下僅能有一個 IsPrimary = 1 的主要實驗室
        builder.HasIndex(x => x.UserId)
            .IsUnique()
            .HasFilter("[IsPrimary] = 1 AND [IsActive] = 1")
            .HasDatabaseName("UX_UserLabMappings_OnePrimaryPerUser");

        // 多租戶（TenantLabId）快速查詢與資料隔離檢索索引
        builder.HasIndex(x => new { x.TenantLabId, x.UserId, x.IsActive })
            .HasDatabaseName("IX_UserLabMappings_TenantLabId_UserId");

        // 兼任與時效過濾檢索索引
        builder.HasIndex(x => new { x.UserId, x.IsActive, x.EffectiveDate, x.ExpiryDate })
            .HasDatabaseName("IX_UserLabMappings_EffectiveRange");
    }
}