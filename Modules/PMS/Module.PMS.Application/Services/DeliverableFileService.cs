using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGS.Modules.PMS.Application.Contracts;
using SGS.Modules.PMS.Application.Rules;
using SGS.Modules.PMS.Infrastructure.Dbcontexts;
using SGS.Modules.PMS.Infrastructure.Entities;
using SGSFramework.Core.FileManagement;
using SGSFramework.Core.Paginations;

namespace SGS.Modules.PMS.Application.Services;

/// <summary>檢視（動態浮水印）內容。</summary>
public sealed record DeliverableView(byte[] Content, string ContentType);

/// <summary>
/// 交付物 × 檔案管理（FMS）整合：由 PMS 直接上傳並登記、浮水印檢視、授權下載與虛擬刪除。
/// <para>
/// 安全模型（沿用 RPT 模組模式）：FMS 分類 <c>pms-deliverable</c> 設為 <c>AllowGenericApi=false</c>，
/// 只能經本服務以 <see cref="FileAccessScope.Internal"/> 存取；本服務先套用專案資料列授權（非成員一律 404），
/// 再代理 FMS。檔案只能經「上傳」建立交付物，不接受外部提供的 FMS 檔案識別碼，避免掛接他人檔案。
/// FMS 負責加密儲存、傳輸簽章驗證與存取稽核（Ledger）。
/// </para>
/// </summary>
public sealed class DeliverableFileService(
    PMSDbContext db,
    IFileManagementService files,
    TimeProvider clock,
    ILogger<DeliverableFileService> logger)
{
    /// <summary>FMS 分類代碼（appsettings FileManagement:Categories）。</summary>
    public const string FileCategory = "pms-deliverable";

    public const string ReferenceType = "PmsDeliverable";

    private const int MaxPageSize = 200;

    // ---------- 上傳 ----------

    public async Task<PmsResult<DeliverableDto>> UploadAsync(
        Guid userId, string actor, Guid projectId, UploadDeliverableMeta meta, Stream content, string originalFileName, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(meta);
        ArgumentNullException.ThrowIfNull(content);

        var project = await LoadProjectAsync(userId, projectId, ct).ConfigureAwait(false);
        if (project.Error is { } accessError) return accessError;
        var p = project.Value!;

        if (p.Status != ProjectStatus.Active)
            return PmsError.Conflict("PMS.INVALID_TRANSITION", "專案已結案或作廢，不可再上傳交付物。");
        if (!Enum.IsDefined(meta.Stage)) return PmsError.Validation("stage 不正確。");
        if (string.IsNullOrWhiteSpace(meta.DocType) || meta.DocType.Trim().Length > 50)
            return PmsError.Validation("docType 為必填且不得超過 50 字。");
        if (string.IsNullOrWhiteSpace(originalFileName))
            return PmsError.Validation("請選擇要上傳的檔案。");

        PmsModule? module = null;
        if (meta.ModuleId is { } moduleId)
        {
            module = p.Modules.FirstOrDefault(m => m.Id == moduleId);
            if (module is null) return PmsError.NotFound("模組不存在。");
        }

        var cleanOriginal = Path.GetFileName(originalFileName.Trim());
        var docName = string.IsNullOrWhiteSpace(meta.DocName) ? Path.GetFileNameWithoutExtension(cleanOriginal) : meta.DocName;

        var generated = DeliverableNaming.Generate(
            TaipeiToday(), docName, meta.Version, actor, Path.GetExtension(cleanOriginal), module?.Code, module?.Name);
        if (!generated.Success)
            return PmsError.Unprocessable("PMS.NAMING_RULE_VIOLATION", generated.Error ?? "無法產生符合規範的檔名。");

        var version = DeliverableNaming.NormalizeVersion(meta.Version)!;
        var docType = meta.DocType.Trim();

        // 同一文件（專案＋模組＋類型）同版本未刪除者唯一：先檢查以免白白上傳；唯一索引兜底併發
        if (await VersionExistsAsync(p.Id, module?.Id, docType, version, ct).ConfigureAwait(false))
            return PmsError.Conflict("PMS.DELIVERABLE_VERSION_EXISTS", $"「{docType}」已存在版本 {version}；請使用新的版本號。");

        var deliverableId = Guid.NewGuid();
        FileMetadataDto stored;
        try
        {
            stored = await files.UploadAsync(
                new FileUploadRequest(content, generated.FileName!, FileCategory, ReferenceType, deliverableId.ToString(), meta.ExpectedSha256),
                FileAccessScope.Internal, ct).ConfigureAwait(false);
        }
        catch (FileRejectedException ex)
        {
            return MapRejected(ex);
        }

        var deliverable = new Deliverable
        {
            Id = deliverableId,
            ProjectId = p.Id,
            ModuleId = module?.Id,
            Stage = meta.Stage,
            DocType = docType,
            DocName = docName.Trim(),
            Version = version,
            FileId = stored.Id,
            FileName = generated.FileName!,
            OriginalFileName = cleanOriginal,
            ContentType = stored.ContentType,
            FileSize = stored.FileSize,
            Sha256 = stored.Sha256,
            CreatedBy = actor,
            CreatedAtUtc = clock.GetUtcNow()
        };

        try
        {
            db.Deliverables.Add(deliverable);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 補償：中繼資料寫入失敗時回收已寫入 FMS 的檔案，避免孤兒檔案
            db.Entry(deliverable).State = EntityState.Detached;
            await CompensateAsync(stored.Id).ConfigureAwait(false);

            if (ex is DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } })
                return PmsError.Conflict("PMS.DELIVERABLE_VERSION_EXISTS", $"「{docType}」已存在版本 {version}；請使用新的版本號。");
            throw;
        }

        return PmsResult<DeliverableDto>.Success(ToDto(deliverable, module, isLatest: true));
    }

    // ---------- 查詢 ----------

    public async Task<PmsResult<PagedResult<DeliverableDto>>> SearchAsync(Guid userId, Guid projectId, DeliverableSearchRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (await AccessAsync(userId, projectId, ct).ConfigureAwait(false) != ProjectAccessLevel.Internal)
            return PmsError.NotFound();

        var pageIndex = Math.Max(1, request.PageIndex);
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);
        var filter = request.Filter ?? new DeliverableSearchFilter();

        // 專案文件量級有限：載入未刪除者於記憶體計算「最新版」（版本號需語意比較，無法以字串排序），再篩選與分頁
        var rows = await db.Deliverables.AsNoTracking().Include(d => d.Module)
            .Where(d => d.ProjectId == projectId && !d.IsDeleted)
            .ToListAsync(ct).ConfigureAwait(false);

        var latestIds = rows
            .GroupBy(d => (d.ModuleId, DocType: d.DocType.ToUpperInvariant()))
            .Select(g => g.OrderByDescending(d => ParseVersion(d.Version)).ThenByDescending(d => d.CreatedAtUtc).First().Id)
            .ToHashSet();

        IEnumerable<Deliverable> query = rows;
        if (!filter.IncludeHistory) query = query.Where(d => latestIds.Contains(d.Id));
        if (filter.Stage is { } stage) query = query.Where(d => d.Stage == stage);
        if (!string.IsNullOrWhiteSpace(filter.DocType))
            query = query.Where(d => d.DocType.Equals(filter.DocType.Trim(), StringComparison.OrdinalIgnoreCase));
        if (filter.ModuleId is { } mid) query = query.Where(d => d.ModuleId == mid);
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(d =>
                d.FileName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (d.OriginalFileName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (d.DocName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                d.DocType.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var filtered = query.OrderByDescending(d => d.CreatedAtUtc).ToList();
        var items = filtered.Skip((pageIndex - 1) * pageSize).Take(pageSize)
            .Select(d => ToDto(d, d.Module, latestIds.Contains(d.Id))).ToList();

        return PmsResult<PagedResult<DeliverableDto>>.Success(new PagedResult<DeliverableDto>(items, filtered.Count, pageIndex, pageSize));
    }

    // ---------- 檢視／下載 ----------

    /// <summary>浮水印檢視（PDF 逐頁點陣化、圖片直接繪製）；僅支援 PDF 與圖片，其他格式回 415。</summary>
    public async Task<PmsResult<DeliverableView>> OpenViewAsync(Guid userId, Guid projectId, Guid deliverableId, string watermarkText, CancellationToken ct)
    {
        var found = await FindWithFileAsync(userId, projectId, deliverableId, ct).ConfigureAwait(false);
        if (found.Error is { } error) return error;

        try
        {
            var view = await files.OpenWatermarkedViewAsync(found.Value!.FileId, watermarkText, FileAccessScope.Internal, ct).ConfigureAwait(false);
            return view is null
                ? PmsError.NotFound("檔案不存在或已被清除。")
                : PmsResult<DeliverableView>.Success(new DeliverableView(view.Content, view.ContentType));
        }
        catch (FileRejectedException ex)
        {
            return MapRejected(ex);
        }
    }

    /// <summary>
    /// 下載原檔：回傳之 <see cref="FileDownload.Content"/> 由呼叫端（FileStreamResult）負責釋放。
    /// 下載權限（PMS.DELIVERABLE.DOWNLOAD）由 Controller 的 [RequiresPermission] 控管。
    /// </summary>
    public async Task<PmsResult<FileDownload>> OpenDownloadAsync(Guid userId, Guid projectId, Guid deliverableId, CancellationToken ct)
    {
        var found = await FindWithFileAsync(userId, projectId, deliverableId, ct).ConfigureAwait(false);
        if (found.Error is { } error) return error;

        try
        {
            var download = await files.OpenDownloadAsync(found.Value!.FileId, FileAccessScope.Internal, ct).ConfigureAwait(false);
            if (download is null) return PmsError.NotFound("檔案不存在或已被清除。");
            if (download.Content is null)
            {
                // pms-deliverable 為地端加密分類，不應產生預簽名導向；若組態誤設為公開則拒絕，避免繞過 PMS 授權
                logger.LogWarning("[PMS] 交付物 {DeliverableId} 的 FMS 分類回傳導向網址，已拒絕。", deliverableId);
                return PmsError.Conflict("PMS.FILE_REJECTED", "此檔案不支援經 PMS 下載，請聯絡管理員檢查 FMS 分類設定。");
            }

            return PmsResult<FileDownload>.Success(download);
        }
        catch (FileRejectedException ex)
        {
            return MapRejected(ex);
        }
    }

    // ---------- 刪除 ----------

    /// <summary>虛擬刪除：僅專案 PM／Tech Lead。FMS 實體檔案於保留期滿後由排程清除。</summary>
    public async Task<PmsResult<bool>> DeleteAsync(Guid userId, string actor, Guid projectId, Guid deliverableId, CancellationToken ct)
    {
        var project = await LoadProjectAsync(userId, projectId, ct).ConfigureAwait(false);
        if (project.Error is { } accessError) return accessError;

        var members = project.Value!.Members.Select(m => (m.UserId, m.RaciRole)).ToList();
        if (!ProjectAccessPolicy.IsPm(userId, members) && !ProjectAccessPolicy.IsTechLead(userId, members))
            return PmsError.Forbidden("僅專案 PM 或 Tech Lead 可刪除交付物。");

        var deliverable = await db.Deliverables.FirstOrDefaultAsync(d => d.Id == deliverableId && d.ProjectId == projectId && !d.IsDeleted, ct).ConfigureAwait(false);
        if (deliverable is null) return PmsError.NotFound("交付物不存在。");

        var now = clock.GetUtcNow();
        deliverable.IsDeleted = true;
        deliverable.DeletedAtUtc = now;
        deliverable.DeletedBy = actor;
        deliverable.UpdatedBy = actor;
        deliverable.UpdatedAtUtc = now;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        if (deliverable.HasFile)
        {
            try
            {
                await files.DeleteAsync(deliverable.FileId, FileAccessScope.Internal, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // 索引已標記刪除、使用者已看不到；FMS 端失敗僅記錄，可由維運清理
                logger.LogError(ex, "[PMS] 交付物 {DeliverableId} 已標記刪除，但 FMS 刪除檔案 {FileId} 失敗。", deliverableId, deliverable.FileId);
            }
        }

        return PmsResult<bool>.Success(true);
    }

    // ---------- 內部輔助 ----------

    private DateOnly TaipeiToday() =>
        DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(8)).DateTime); // 台灣無日光節約，固定 +8

    private async Task<bool> VersionExistsAsync(Guid projectId, Guid? moduleId, string docType, string version, CancellationToken ct) =>
        await db.Deliverables.AsNoTracking().AnyAsync(
            d => d.ProjectId == projectId && d.ModuleId == moduleId && d.DocType == docType && d.Version == version && !d.IsDeleted, ct)
            .ConfigureAwait(false);

    private async Task CompensateAsync(Guid fileId)
    {
        try
        {
            // 補償不可被呼叫端的取消中斷
            await files.DeleteAsync(fileId, FileAccessScope.Internal, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[PMS] 交付物中繼資料寫入失敗，且回收 FMS 檔案 {FileId} 失敗，請維運清理孤兒檔案。", fileId);
        }
    }

    private async Task<ProjectAccessLevel> AccessAsync(Guid userId, Guid projectId, CancellationToken ct)
    {
        var members = await db.ProjectMembers.AsNoTracking()
            .Where(m => m.ProjectId == projectId).Select(m => new { m.UserId, m.RaciRole }).ToListAsync(ct).ConfigureAwait(false);
        return ProjectAccessPolicy.Evaluate(userId, members.Select(m => (m.UserId, m.RaciRole)));
    }

    /// <summary>載入含成員與模組的專案（唯讀）；非內部成員一律 404（Stakeholder 不可管理交付物）。</summary>
    private async Task<PmsResult<Project>> LoadProjectAsync(Guid userId, Guid projectId, CancellationToken ct)
    {
        var project = await db.Projects.AsNoTracking().Include(p => p.Members).Include(p => p.Modules)
            .FirstOrDefaultAsync(p => p.Id == projectId, ct).ConfigureAwait(false);
        if (project is null) return PmsError.NotFound();

        var access = ProjectAccessPolicy.Evaluate(userId, project.Members.Select(m => (m.UserId, m.RaciRole)));
        return access == ProjectAccessLevel.Internal ? PmsResult<Project>.Success(project) : PmsError.NotFound();
    }

    private async Task<PmsResult<Deliverable>> FindWithFileAsync(Guid userId, Guid projectId, Guid deliverableId, CancellationToken ct)
    {
        if (await AccessAsync(userId, projectId, ct).ConfigureAwait(false) != ProjectAccessLevel.Internal)
            return PmsError.NotFound();

        var d = await db.Deliverables.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == deliverableId && x.ProjectId == projectId && !x.IsDeleted, ct).ConfigureAwait(false);
        if (d is null) return PmsError.NotFound("交付物不存在。");
        if (!d.HasFile) return PmsError.NotFound("此交付物沒有可檢視或下載的檔案（舊版僅登記索引）。");
        return PmsResult<Deliverable>.Success(d);
    }

    private static PmsError MapRejected(FileRejectedException ex) => ex.StatusCode switch
    {
        503 => new PmsError("PMS.FILE_SERVICE_UNAVAILABLE", 503, "Service Unavailable", ex.Message),
        413 => new PmsError("PMS.FILE_REJECTED", 413, "Payload Too Large", ex.Message),
        415 => new PmsError("PMS.FILE_REJECTED", 415, "Unsupported Media Type", ex.Message),
        403 => new PmsError("PMS.FILE_REJECTED", 403, "Forbidden", ex.Message),
        _ => new PmsError("PMS.FILE_REJECTED", 400, "Invalid Request", ex.Message)
    };

    /// <summary>版本號語意比較（v1.10 &gt; v1.9）；無法解析者視為最小。</summary>
    internal static Version ParseVersion(string value)
    {
        var text = value.TrimStart('v', 'V');
        return Version.TryParse(text, out var v) ? v : new Version(0, 0);
    }

    private static DeliverableDto ToDto(Deliverable d, PmsModule? module, bool isLatest)
    {
        var canPreview = d.ContentType is { } ct &&
            (ct.StartsWith("image/", StringComparison.OrdinalIgnoreCase) || ct.Equals("application/pdf", StringComparison.OrdinalIgnoreCase));

        return new DeliverableDto(
            d.Id, d.Stage, d.DocType, d.DocName, d.Version, d.FileName, d.OriginalFileName, d.FileSize, d.ContentType,
            d.HasFile, d.HasFile && canPreview, isLatest, d.ModuleId, module?.Code,
            ProjectFolderMap.Resolve(d.Stage, d.DocType, module?.Code, module?.Name), d.CreatedBy, d.CreatedAtUtc);
    }
}
