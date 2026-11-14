using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGS.Modules.PMS.Application.Contracts;
using SGS.Modules.PMS.Application.Rules;
using SGS.Modules.PMS.Infrastructure.Dbcontexts;
using SGS.Modules.PMS.Infrastructure.Entities;
using SGSFramework.Core.FileManagement;

namespace SGS.Modules.PMS.Application.Services;

/// <summary>
/// 問題單附件 × 檔案管理（FMS）：截圖、Log 等由 PMS 直接上傳（分類 <c>pms-issue</c>，加密儲存、僅經 PMS 授權後存取），
/// 檔名由系統依 SOP 5.5 產生（<c>YYYYMMDD_[單號]_檔案說明.ext</c>）。上傳後預設為「待遮罩確認」（SOP 8-4：個資、密碼、金鑰須先遮罩）。
/// 安全模型同 <see cref="DeliverableFileService"/>：先套用問題單可見性（非成員一律 404）再代理 FMS（Internal）。
/// </summary>
public sealed class TicketAttachmentService(
    PMSDbContext db,
    TicketService tickets,
    IFileManagementService files,
    TimeProvider clock,
    ILogger<TicketAttachmentService> logger)
{
    public const string FileCategory = "pms-issue";

    public const string ReferenceType = "PmsTicket";

    // ---------- 上傳 ----------

    public async Task<PmsResult<TicketAttachmentDto>> UploadAsync(
        Guid userId, string actor, Guid ticketId, string? description, Stream content, string originalFileName, string? expectedSha256, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(content);

        var ctx = await tickets.LoadAsync(userId, ticketId, track: false, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var t = ctx.Value!.Ticket;

        if (t.Status is TicketStatus.Closed or TicketStatus.Cancelled)
            return PmsError.Conflict("PMS.INVALID_TRANSITION", "問題單已結案或取消，不可再上傳附件。");
        if (string.IsNullOrWhiteSpace(originalFileName)) return PmsError.Validation("請選擇要上傳的檔案。");

        var cleanOriginal = Path.GetFileName(originalFileName.Trim());
        var name = DeliverableNaming.GenerateIssue(
            TaipeiToday(), t.TicketNo,
            string.IsNullOrWhiteSpace(description) ? Path.GetFileNameWithoutExtension(cleanOriginal) : description,
            Path.GetExtension(cleanOriginal));
        if (!name.Success) return PmsError.Unprocessable("PMS.NAMING_RULE_VIOLATION", name.Error ?? "無法產生符合規範的檔名。");

        var attachmentId = Guid.NewGuid();
        FileMetadataDto stored;
        try
        {
            stored = await files.UploadAsync(
                new FileUploadRequest(content, name.FileName!, FileCategory, ReferenceType, t.Id.ToString(), expectedSha256),
                FileAccessScope.Internal, ct).ConfigureAwait(false);
        }
        catch (FileRejectedException ex)
        {
            return MapRejected(ex);
        }

        var now = clock.GetUtcNow();
        var attachment = new TicketAttachment
        {
            Id = attachmentId,
            TicketId = t.Id,
            FileId = stored.Id,
            FileName = name.FileName!,
            OriginalFileName = cleanOriginal,
            ContentType = stored.ContentType,
            FileSize = stored.FileSize,
            Sha256 = stored.Sha256,
            MaskConfirmed = false,
            CreatedBy = actor,
            CreatedAtUtc = now
        };

        try
        {
            db.TicketAttachments.Add(attachment);
            db.TicketEvents.Add(TicketService.NewEvent(t.Id, TicketEventType.AttachmentAdded, $"新增附件：{attachment.FileName}", userId, actor, now));
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 補償：中繼資料寫入失敗時回收已寫入 FMS 的檔案
            db.ChangeTracker.Clear();
            await CompensateAsync(stored.Id).ConfigureAwait(false);
            throw;
        }

        return PmsResult<TicketAttachmentDto>.Success(ToDto(attachment));
    }

    // ---------- 檢視／下載 ----------

    public async Task<PmsResult<DeliverableView>> OpenViewAsync(Guid userId, Guid ticketId, Guid attachmentId, string watermarkText, CancellationToken ct)
    {
        var found = await FindAsync(userId, ticketId, attachmentId, ct).ConfigureAwait(false);
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

    public async Task<PmsResult<FileDownload>> OpenDownloadAsync(Guid userId, Guid ticketId, Guid attachmentId, CancellationToken ct)
    {
        var found = await FindAsync(userId, ticketId, attachmentId, ct).ConfigureAwait(false);
        if (found.Error is { } error) return error;

        try
        {
            var download = await files.OpenDownloadAsync(found.Value!.FileId, FileAccessScope.Internal, ct).ConfigureAwait(false);
            if (download is null) return PmsError.NotFound("檔案不存在或已被清除。");
            if (download.Content is null)
            {
                logger.LogWarning("[PMS] 問題單附件 {AttachmentId} 的 FMS 分類回傳導向網址，已拒絕。", attachmentId);
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

    /// <summary>確認附件已遮罩個資、密碼與金鑰（SOP 8-4）。僅內部成員。</summary>
    public async Task<PmsResult<TicketAttachmentDto>> ConfirmMaskAsync(Guid userId, string actor, Guid ticketId, Guid attachmentId, CancellationToken ct)
    {
        var ctx = await tickets.LoadAsync(userId, ticketId, track: false, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        if (ctx.Value!.Access != ProjectAccessLevel.Internal) return PmsError.NotFound();

        var attachment = await db.TicketAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId && a.TicketId == ticketId && !a.IsDeleted, ct).ConfigureAwait(false);
        if (attachment is null) return PmsError.NotFound("附件不存在。");

        attachment.MaskConfirmed = true;
        attachment.UpdatedBy = actor;
        attachment.UpdatedAtUtc = clock.GetUtcNow();
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return PmsResult<TicketAttachmentDto>.Success(ToDto(attachment));
    }

    /// <summary>虛擬刪除附件：僅專案 PM／Tech Lead。</summary>
    public async Task<PmsResult<bool>> DeleteAsync(Guid userId, string actor, Guid ticketId, Guid attachmentId, CancellationToken ct)
    {
        var ctx = await tickets.LoadAsync(userId, ticketId, track: false, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!;

        if (c.Access != ProjectAccessLevel.Internal) return PmsError.NotFound();
        if (!ProjectAccessPolicy.IsPm(userId, c.Members) && !ProjectAccessPolicy.IsTechLead(userId, c.Members))
            return PmsError.Forbidden("僅專案 PM 或 Tech Lead 可刪除附件。");

        var attachment = await db.TicketAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId && a.TicketId == ticketId && !a.IsDeleted, ct).ConfigureAwait(false);
        if (attachment is null) return PmsError.NotFound("附件不存在。");

        var now = clock.GetUtcNow();
        attachment.IsDeleted = true;
        attachment.UpdatedBy = actor;
        attachment.UpdatedAtUtc = now;
        db.TicketEvents.Add(TicketService.NewEvent(ticketId, TicketEventType.Comment, $"刪除附件：{attachment.FileName}", userId, actor, now));
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        try
        {
            await files.DeleteAsync(attachment.FileId, FileAccessScope.Internal, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "[PMS] 附件 {AttachmentId} 已標記刪除，但 FMS 刪除檔案 {FileId} 失敗。", attachmentId, attachment.FileId);
        }

        return PmsResult<bool>.Success(true);
    }

    // ---------- 內部輔助 ----------

    private async Task<PmsResult<TicketAttachment>> FindAsync(Guid userId, Guid ticketId, Guid attachmentId, CancellationToken ct)
    {
        var ctx = await tickets.LoadAsync(userId, ticketId, track: false, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;

        var attachment = ctx.Value!.Ticket.Attachments.FirstOrDefault(a => a.Id == attachmentId && !a.IsDeleted);
        return attachment is null ? PmsError.NotFound("附件不存在。") : PmsResult<TicketAttachment>.Success(attachment);
    }

    private async Task CompensateAsync(Guid fileId)
    {
        try
        {
            await files.DeleteAsync(fileId, FileAccessScope.Internal, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[PMS] 附件中繼資料寫入失敗，且回收 FMS 檔案 {FileId} 失敗，請維運清理孤兒檔案。", fileId);
        }
    }

    private DateOnly TaipeiToday() =>
        DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(SlaCalculator.Offset).DateTime);

    private static TicketAttachmentDto ToDto(TicketAttachment a) => new(
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
