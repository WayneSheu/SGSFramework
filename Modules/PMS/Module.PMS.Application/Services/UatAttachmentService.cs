using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGS.Modules.PMS.Application.Contracts;
using SGS.Modules.PMS.Application.Rules;
using SGS.Modules.PMS.Infrastructure.Dbcontexts;
using SGS.Modules.PMS.Infrastructure.Entities;
using SGSFramework.Core.FileManagement;

namespace SGS.Modules.PMS.Application.Services;

/// <summary>
/// UAT 驗收簽核單附件 × 檔案管理（FMS）：客戶回覆信件、簽署掃描檔、驗收截圖等由 PMS 直接上傳（分類 <c>pms-issue</c>，加密儲存、
/// 僅經 PMS 授權後存取），檔名由系統產生（<c>YYYYMMDD_專案代號_UAT_版本_檔案說明.ext</c>），上傳後預設為「待遮罩確認」（SOP 8-4）。
/// 授權：專案內部成員可檢視／下載；上傳、確認遮罩、刪除限專案 PM／Tech Lead；已取消的驗收單不可再上傳，
/// 刪除僅限待簽核（驗收結果記錄後，證據附件鎖定）。非內部成員一律 404。
/// </summary>
public sealed class UatAttachmentService(
    PMSDbContext db,
    IFileManagementService files,
    TimeProvider clock,
    ILogger<UatAttachmentService> logger)
{
    public const string FileCategory = "pms-issue";

    public const string ReferenceType = "PmsUat";

    /// <summary>每張驗收單的有效附件數上限（防濫用，不含已刪除）。</summary>
    public const int MaxAttachmentsPerUat = 50;

    // ---------- 上傳 ----------

    public async Task<PmsResult<UatAttachmentDto>> UploadAsync(
        Guid userId, string actor, Guid uatId, string? description, Stream content, string originalFileName, string? expectedSha256, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(content);

        var ctx = await LoadAsync(userId, uatId, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!;

        if (!UatService.CanManageAttachments(userId, c.Members)) return PmsError.Forbidden("僅專案 PM 或 Tech Lead 可上傳驗收附件。");
        if (c.Uat.Status == UatStatus.Cancelled) return PmsError.Conflict("PMS.INVALID_TRANSITION", "驗收簽核單已取消，不可再上傳附件。");
        if (string.IsNullOrWhiteSpace(originalFileName)) return PmsError.Validation("請選擇要上傳的檔案。");

        var existing = await db.UatAttachments.CountAsync(a => a.UatSignoffId == uatId && !a.IsDeleted, ct).ConfigureAwait(false);
        if (existing >= MaxAttachmentsPerUat) return PmsError.Validation($"每張驗收簽核單最多 {MaxAttachmentsPerUat} 個附件。");

        var cleanOriginal = Path.GetFileName(originalFileName.Trim());
        var name = DeliverableNaming.GenerateUat(
            TaipeiToday(), c.Project.Code, c.Uat.Version,
            string.IsNullOrWhiteSpace(description) ? Path.GetFileNameWithoutExtension(cleanOriginal) : description,
            Path.GetExtension(cleanOriginal));
        if (!name.Success) return PmsError.Unprocessable("PMS.NAMING_RULE_VIOLATION", name.Error ?? "無法產生符合規範的檔名。");

        FileMetadataDto stored;
        try
        {
            stored = await files.UploadAsync(
                new FileUploadRequest(content, name.FileName!, FileCategory, ReferenceType, uatId.ToString(), expectedSha256),
                FileAccessScope.Internal, ct).ConfigureAwait(false);
        }
        catch (FileRejectedException ex)
        {
            return MapRejected(ex);
        }

        var attachment = new UatAttachment
        {
            Id = Guid.NewGuid(),
            UatSignoffId = uatId,
            FileId = stored.Id,
            FileName = name.FileName!,
            OriginalFileName = cleanOriginal,
            ContentType = stored.ContentType,
            FileSize = stored.FileSize,
            Sha256 = stored.Sha256,
            MaskConfirmed = false,
            CreatedBy = actor,
            CreatedAtUtc = clock.GetUtcNow()
        };

        try
        {
            db.UatAttachments.Add(attachment);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 補償：中繼資料寫入失敗時回收已寫入 FMS 的檔案
            db.ChangeTracker.Clear();
            await CompensateAsync(stored.Id).ConfigureAwait(false);
            throw;
        }

        return PmsResult<UatAttachmentDto>.Success(ToDto(attachment));
    }

    // ---------- 檢視／下載 ----------

    public async Task<PmsResult<DeliverableView>> OpenViewAsync(Guid userId, Guid uatId, Guid attachmentId, string watermarkText, CancellationToken ct)
    {
        var found = await FindAsync(userId, uatId, attachmentId, track: false, ct).ConfigureAwait(false);
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

    public async Task<PmsResult<FileDownload>> OpenDownloadAsync(Guid userId, Guid uatId, Guid attachmentId, CancellationToken ct)
    {
        var found = await FindAsync(userId, uatId, attachmentId, track: false, ct).ConfigureAwait(false);
        if (found.Error is { } error) return error;

        try
        {
            var download = await files.OpenDownloadAsync(found.Value!.FileId, FileAccessScope.Internal, ct).ConfigureAwait(false);
            if (download is null) return PmsError.NotFound("檔案不存在或已被清除。");
            if (download.Content is null)
            {
                logger.LogWarning("[PMS] UAT 附件 {AttachmentId} 的 FMS 分類回傳導向網址，已拒絕。", attachmentId);
                return PmsError.Conflict("PMS.FILE_REJECTED", "此檔案不支援經 PMS 下載，請聯絡管理員檢查 FMS 分類設定。");
            }

            return PmsResult<FileDownload>.Success(download);
        }
        catch (FileRejectedException ex)
        {
            return MapRejected(ex);
        }
    }

    // ---------- 遮罩確認／刪除 ----------

    public async Task<PmsResult<UatAttachmentDto>> ConfirmMaskAsync(Guid userId, string actor, Guid uatId, Guid attachmentId, CancellationToken ct)
    {
        var ctx = await LoadAsync(userId, uatId, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        if (!UatService.CanManageAttachments(userId, ctx.Value!.Members)) return PmsError.Forbidden("僅專案 PM 或 Tech Lead 可確認遮罩。");

        var attachment = await db.UatAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId && a.UatSignoffId == uatId && !a.IsDeleted, ct).ConfigureAwait(false);
        if (attachment is null) return PmsError.NotFound("附件不存在。");

        attachment.MaskConfirmed = true;
        attachment.UpdatedBy = actor;
        attachment.UpdatedAtUtc = clock.GetUtcNow();
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return PmsResult<UatAttachmentDto>.Success(ToDto(attachment));
    }

    /// <summary>虛擬刪除附件：僅專案 PM／Tech Lead，且僅限待簽核（驗收結果記錄後，證據附件鎖定）。</summary>
    public async Task<PmsResult<bool>> DeleteAsync(Guid userId, string actor, Guid uatId, Guid attachmentId, CancellationToken ct)
    {
        var ctx = await LoadAsync(userId, uatId, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!;

        if (!UatService.CanManageAttachments(userId, c.Members)) return PmsError.Forbidden("僅專案 PM 或 Tech Lead 可刪除附件。");
        if (c.Uat.Status != UatStatus.PendingSign)
            return PmsError.Conflict("PMS.CONTENT_LOCKED", "驗收結果已記錄或已取消，證據附件鎖定，不可刪除。");

        var attachment = await db.UatAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId && a.UatSignoffId == uatId && !a.IsDeleted, ct).ConfigureAwait(false);
        if (attachment is null) return PmsError.NotFound("附件不存在。");

        attachment.IsDeleted = true;
        attachment.UpdatedBy = actor;
        attachment.UpdatedAtUtc = clock.GetUtcNow();
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        try
        {
            await files.DeleteAsync(attachment.FileId, FileAccessScope.Internal, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "[PMS] UAT 附件 {AttachmentId} 已標記刪除，但 FMS 刪除檔案 {FileId} 失敗。", attachmentId, attachment.FileId);
        }

        return PmsResult<bool>.Success(true);
    }

    // ---------- 內部輔助 ----------

    private sealed record Context(UatSignoff Uat, Project Project, IReadOnlyList<(Guid UserId, RaciRole Role)> Members);

    private async Task<PmsResult<Context>> LoadAsync(Guid userId, Guid uatId, CancellationToken ct)
    {
        var uat = await db.UatSignoffs.AsNoTracking().FirstOrDefaultAsync(u => u.Id == uatId, ct).ConfigureAwait(false);
        if (uat is null) return PmsError.NotFound();

        var project = await db.Projects.AsNoTracking().Include(p => p.Members).FirstOrDefaultAsync(p => p.Id == uat.ProjectId, ct).ConfigureAwait(false);
        if (project is null) return PmsError.NotFound();

        var members = project.Members.Select(m => (m.UserId, m.RaciRole)).ToList();
        if (ProjectAccessPolicy.Evaluate(userId, members) != ProjectAccessLevel.Internal) return PmsError.NotFound();

        return PmsResult<Context>.Success(new Context(uat, project, members));
    }

    private async Task<PmsResult<UatAttachment>> FindAsync(Guid userId, Guid uatId, Guid attachmentId, bool track, CancellationToken ct)
    {
        var ctx = await LoadAsync(userId, uatId, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;

        var query = track ? db.UatAttachments.AsQueryable() : db.UatAttachments.AsNoTracking();
        var attachment = await query.FirstOrDefaultAsync(a => a.Id == attachmentId && a.UatSignoffId == uatId && !a.IsDeleted, ct).ConfigureAwait(false);
        return attachment is null ? PmsError.NotFound("附件不存在。") : PmsResult<UatAttachment>.Success(attachment);
    }

    private async Task CompensateAsync(Guid fileId)
    {
        try
        {
            await files.DeleteAsync(fileId, FileAccessScope.Internal, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[PMS] UAT 附件中繼資料寫入失敗，且回收 FMS 檔案 {FileId} 失敗，請維運清理孤兒檔案。", fileId);
        }
    }

    private DateOnly TaipeiToday() =>
        DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(SlaCalculator.Offset).DateTime);

    internal static UatAttachmentDto ToDto(UatAttachment a) => new(
        a.Id, a.FileName, a.OriginalFileName, a.ContentType, a.FileSize, a.MaskConfirmed,
        a.ContentType is { } ct && (ct.StartsWith("image/", StringComparison.OrdinalIgnoreCase) || ct.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)),
        a.CreatedBy, a.CreatedAtUtc);

    private static PmsError MapRejected(FileRejectedException ex) => ex.StatusCode switch
    {
        503 => new PmsError("PMS.FILE_SERVICE_UNAVAILABLE", 503, "Service Unavailable", ex.Message),
        413 => new PmsError("PMS.FILE_REJECTED", 413, "Payload Too Large", ex.Message),
        415 => new PmsError("PMS.FILE_REJECTED", 415, "Unsupported Media Type", ex.Message),
        403 => new PmsError("PMS.FILE_REJECTED", 403, "Forbidden", ex.Message),
        _ => new PmsError("PMS.FILE_REJECTED", 400, "Invalid Request", ex.Message)
    };
}
