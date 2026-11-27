using SGSFramework.Core.Abstractions.Entities.ReportCategories;
using SGSFramework.Core.Abstractions.Permissions.Entities;

namespace SGSFramework.AuthTokenBucket.Services;

public sealed record ReportCategoryDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    ConfidentialityLevel DefaultConfidentiality,
    string? RegulationTags,
    int DisplayOrder,
    bool IsActive,
    IReadOnlyList<Guid> TenantLabIds,
    string RowVersion,
    ReportCategoryScope Scope,
    /// <summary>主責實驗室（建立時第一個所屬實驗室）；實驗室專屬類別的完整所屬實驗室為 TenantLabIds</summary>
    Guid? OwnerTenantLabId,
    string? StorageCategory,
    string? StorageFolder,
    string EffectiveStorageCategory,
    string EffectiveStorageFolder,
    bool EncryptAtRest,
    bool WatermarkOnDownload,
    bool TamperVerification,
    bool CanManage);

/// <summary>
/// 新增（Id 為 null）或更新報告類別；類別代碼建立後不可變更。
/// 實驗室專屬類別須指定所屬實驗室（OwnerTenantLabIds，可多個；舊呼叫端可只給 OwnerTenantLabId），承作實驗室固定為所屬實驗室。
/// </summary>
public sealed record SaveReportCategoryRequest(
    Guid? Id,
    string Code,
    string Name,
    string? Description,
    ConfidentialityLevel DefaultConfidentiality,
    string? RegulationTags,
    int DisplayOrder,
    bool IsActive,
    string? RowVersion,
    ReportCategoryScope Scope = ReportCategoryScope.Global,
    Guid? OwnerTenantLabId = null,
    string? StorageCategory = null,
    string? StorageFolder = null,
    bool EncryptAtRest = true,
    bool WatermarkOnDownload = false,
    bool TamperVerification = true,
    IReadOnlyList<Guid>? OwnerTenantLabIds = null);

/// <summary>
/// 報告類別分頁查詢（POST report-categories/search）。PageIndex 由 1 起算；
/// FocusCode 有值時改回傳該類別所在的頁（存檔後定位新資料列用），找不到時 FocusFound = false 並回傳 PageIndex 指定的頁。
/// </summary>
public sealed class ReportCategorySearchRequest
{
    /// <summary>頁碼（由 1 起算）</summary>
    public int PageIndex { get; init; } = 1;

    /// <summary>每頁筆數（1～200，預設 20）</summary>
    public int PageSize { get; init; } = 20;

    /// <summary>類別代碼（包含比對）</summary>
    public string? Code { get; init; }

    /// <summary>類別名稱（包含比對）</summary>
    public string? Name { get; init; }

    /// <summary>關鍵字：說明、法規標籤、儲存分類、儲存資料夾</summary>
    public string? Keyword { get; init; }

    /// <summary>適用範圍：0 = 全域、1 = 實驗室專屬；不指定為全部</summary>
    public ReportCategoryScope? Scope { get; init; }

    /// <summary>啟用狀態：true = 啟用中、false = 已停用；不指定為全部</summary>
    public bool? IsActive { get; init; }

    /// <summary>排序欄位：code（代碼）/ name（名稱）/ scope（範圍）/ status（狀態）；空白為「排序值 + 代碼」</summary>
    public string? SortBy { get; init; }

    /// <summary>是否遞減排序</summary>
    public bool SortDescending { get; init; }

    /// <summary>定位用類別代碼：有值時改回傳該類別所在的頁（存檔後顯示新資料列）</summary>
    public string? FocusCode { get; init; }
}

/// <summary>報告類別分頁結果（另附全體類別數與啟用數供工具列摘要）</summary>
/// <param name="Items">本頁的報告類別</param>
/// <param name="TotalCount">符合篩選條件的總筆數</param>
/// <param name="PageIndex">實際回傳的頁碼（由 1 起算；超出範圍時修正為最後一頁，定位時為目標所在頁）</param>
/// <param name="PageSize">每頁筆數</param>
/// <param name="AllCount">全部報告類別數（不受篩選影響）</param>
/// <param name="ActiveCount">啟用中的報告類別數（不受篩選影響）</param>
/// <param name="FocusFound">定位的類別是否位於目前篩選結果中</param>
public sealed record ReportCategorySearchResult(
    IReadOnlyList<ReportCategoryDto> Items,
    int TotalCount,
    int PageIndex,
    int PageSize,
    int AllCount,
    int ActiveCount,
    bool FocusFound);

/// <summary>報告類別儲存與資安設定的預設值（RPT 模組與管理介面共用）</summary>
public static class ReportCategoryStorageDefaults
{
    /// <summary>未指定儲存分類時使用的檔案管理分類</summary>
    public const string StorageCategory = "Report";

    /// <summary>未指定資料夾時的儲存鍵前綴</summary>
    public static string FolderFor(string categoryCode) => $"reports/{categoryCode}";
}

public sealed record SetCategoryLabsRequest(List<Guid> TenantLabIds);

public sealed record ReportLabOptionDto(Guid TenantLabId, string Code, string Name);

public sealed record ReportPrincipalOptionDto(GrantPrincipalType Type, Guid Id, string Name, string? Detail);

public sealed record ReportCategoryGrantDto(
    Guid Id,
    GrantPrincipalType PrincipalType,
    Guid PrincipalId,
    string PrincipalName,
    string CategoryCode,
    Guid? TenantLabId,
    string? LabName,
    ReportActions Actions,
    ReportDataScope DataScope,
    ConfidentialityLevel MaxConfidentiality,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string? Reason,
    string Status,
    string CreatedBy,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? RevokedAt,
    string? RevokedByName,
    string? RevokeReason);

/// <summary>使用者報告類別有效權限總覽（依所屬實驗室分組）</summary>
public sealed record UserReportAccessDto(Guid UserId, IReadOnlyList<UserReportAccessLabDto> Labs);

/// <summary>使用者於單一實驗室的報告類別權限</summary>
public sealed record UserReportAccessLabDto(
    Guid TenantLabId,
    string LabName,
    bool IsPrimary,
    IReadOnlyList<UserReportAccessCategoryDto> Categories);

/// <summary>
/// 單一類別的有效權限：EffectiveActions 為「生效中授權」聯集，且僅於類別啟用、實驗室承作此類別時有效；
/// Sources 列出所有來源（直接 / 角色 / 實驗室），含尚未生效者供管理者對照。
/// </summary>
public sealed record UserReportAccessCategoryDto(
    string CategoryCode,
    string CategoryName,
    bool CategoryActive,
    bool LabHandlesCategory,
    ReportActions EffectiveActions,
    ReportDataScope? EffectiveScope,
    ConfidentialityLevel? EffectiveMaxConfidentiality,
    bool IsSigner,
    string? SignerCertificateNo,
    DateTimeOffset? SignerValidTo,
    IReadOnlyList<UserReportAccessSourceDto> Sources);

/// <summary>授權來源</summary>
public sealed record UserReportAccessSourceDto(
    Guid GrantId,
    GrantPrincipalType SourceType,
    string SourceName,
    ReportActions Actions,
    ReportDataScope DataScope,
    ConfidentialityLevel MaxConfidentiality,
    Guid? TenantLabId,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string Status);

public sealed record CreateReportCategoryGrantRequest(
    GrantPrincipalType PrincipalType,
    Guid PrincipalId,
    string CategoryCode,
    Guid? TenantLabId,
    ReportActions Actions,
    ReportDataScope DataScope,
    ConfidentialityLevel MaxConfidentiality,
    DateTimeOffset? EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string? Reason);

public sealed record SignerQualificationDto(
    Guid Id,
    Guid UserId,
    string UserName,
    string FullName,
    string CategoryCode,
    Guid TenantLabId,
    string LabName,
    string CertificateNo,
    DateTimeOffset ValidFrom,
    DateTimeOffset ValidTo,
    string? Remark,
    string Status,
    string CreatedBy,
    DateTimeOffset? RevokedAt,
    string? RevokedByName,
    string? RevokeReason);

public sealed record CreateSignerQualificationRequest(
    Guid UserId,
    string CategoryCode,
    Guid TenantLabId,
    string CertificateNo,
    DateTimeOffset ValidFrom,
    DateTimeOffset ValidTo,
    string? Remark,
    string? Reason);

public sealed record ReportGrantChangeRequestDto(
    Guid Id,
    ReportGrantChangeType RequestType,
    string Summary,
    Guid? SubjectUserId,
    string? RequestReason,
    RoleChangeRequestStatus Status,
    string RequestedByName,
    DateTimeOffset RequestedAt,
    string? ReviewedByName,
    DateTimeOffset? ReviewedAt,
    string? ReviewComment,
    bool CanApprove,
    bool IsMine);

/// <summary>授權送出結果：敏感授權轉為待審核（PendingApproval），其餘立即生效</summary>
public sealed record ReportGrantSubmitResult(bool Succeeded, string Message, bool PendingApproval = false, Guid? EntityId = null)
{
    public static ReportGrantSubmitResult Fail(string message) => new(false, message);
}

/// <summary>報告類別維護範圍：CanManageAll 可維護全部；否則僅能維護 ManageableLabId 的實驗室專屬類別（null 表示不可維護）</summary>
public sealed record ReportCategoryManageScopeDto(bool CanManageAll, Guid? ManageableLabId);
