using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGS.Modules.PMS.Application.Contracts;
using SGS.Modules.PMS.Application.Rules;
using SGS.Modules.PMS.Infrastructure.Dbcontexts;
using SGS.Modules.PMS.Infrastructure.Entities;
using SGSFramework.Core.FileManagement;

namespace SGS.Modules.PMS.Application.Services;

/// <summary>
/// 合約附件 × 檔案管理（FMS）：甲方合約草稿、用印正本等由 PMS 直接上傳（分類 <c>pms-contract</c>，加密儲存、僅經 PMS 授權後存取）。
/// 檔名由系統產生（<c>YYYYMMDD_合約編號_類型_說明.ext</c>）。授權：合約成員可檢視／下載（檢視加動態浮水印）；上傳與刪除限業務／合約 PM；
/// 用印正本一經上傳即鎖定，不可刪除；非成員一律 404。
/// </summary>
public sealed class ContractAttachmentService(
    PMSDbContext db,
    IFileManagementService files,
    TimeProvider clock,
    ILogger<ContractAttachmentService> logger)
{
    public const string FileCategory = "pms-contract";

    public const string ReferenceType = "PmsContract";

    // ---------- 上傳 ----------

    public async Task<PmsResult<ContractAttachmentDto>> UploadAsync(
        Guid userId, string actor, Guid contractId, ContractAttachmentKind kind, string? description, Stream content, string originalFileName,
        string? expectedSha256, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(content);

        var ctx = await LoadAsync(userId, contractId, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!;

        if (!ContractRules.CanManage(userId, Members(c))) return PmsError.Forbidden("僅合約的業務或合約 PM 可上傳附件。");
        if (c.Status is ContractStatus.Closed or ContractStatus.Lost or ContractStatus.Cancelled or ContractStatus.Terminated)
            return PmsError.Conflict("PMS.INVALID_TRANSITION", "合約已結束，不可再上傳附件。");
        if (!Enum.IsDefined(kind)) return PmsError.Validation("附件類型不正確。");
        if (kind == ContractAttachmentKind.Signed && c.Status is not (ContractStatus.Active or ContractStatus.Accepted or ContractStatus.Warranty))
            return PmsError.Validation("用印正本須在簽約完成後上傳。");
        if (string.IsNullOrWhiteSpace(originalFileName)) return PmsError.Validation("請選擇要上傳的檔案。");

        var existing = await db.ContractAttachments.CountAsync(a => a.ContractId == contractId && !a.IsDeleted, ct).ConfigureAwait(false);
        if (existing >= ContractRules.MaxAttachmentsPerContract) return PmsError.Validation($"每份合約最多 {ContractRules.MaxAttachmentsPerContract} 個附件。");

        var cleanOriginal = Path.GetFileName(originalFileName.Trim());
        var systemName = GenerateName(TaipeiToday(), c.ContractNo, kind, string.IsNullOrWhiteSpace(description) ? Path.GetFileNameWithoutExtension(cleanOriginal) : description, Path.GetExtension(cleanOriginal));

        FileMetadataDto stored;
        try
        {
            stored = await files.UploadAsync(
                new FileUploadRequest(content, systemName, FileCategory, ReferenceType, contractId.ToString(), expectedSha256),
                FileAccessScope.Internal, ct).ConfigureAwait(false);
        }
        catch (FileRejectedException ex)
        {
            return MapRejected(ex);
        }

        var now = clock.GetUtcNow();
        var attachment = new ContractAttachment
        {
            Id = Guid.NewGuid(), ContractId = contractId, FileId = stored.Id, FileName = systemName, OriginalFileName = cleanOriginal,
            ContentType = stored.ContentType, FileSize = stored.FileSize, Sha256 = stored.Sha256, Kind = kind, CreatedBy = actor, CreatedAtUtc = now
        };

        try
        {
            db.ContractAttachments.Add(attachment);
            db.ContractEvents.Add(new ContractEvent
            {
                Id = Guid.NewGuid(), ContractId = contractId, Type = ContractEventType.AttachmentAdded,
                Message = $"上傳附件（{KindName(kind)}）{cleanOriginal}", ActorUserId = userId, ActorName = actor, At = now
            });
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 補償：中繼資料寫入失敗時回收已寫入 FMS 的檔案
            db.ChangeTracker.Clear();
            await CompensateAsync(stored.Id).ConfigureAwait(false);
            throw;
        }

        return PmsResult<ContractAttachmentDto>.Success(ToDto(attachment));
    }

    // ---------- 檢視／下載 ----------

    public async Task<PmsResult<DeliverableView>> OpenViewAsync(Guid userId, Guid contractId, Guid attachmentId, string watermarkText, CancellationToken ct)
    {
        var found = await FindAsync(userId, contractId, attachmentId, ct).ConfigureAwait(false);
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

    public async Task<PmsResult<FileDownload>> OpenDownloadAsync(Guid userId, Guid contractId, Guid attachmentId, CancellationToken ct)
    {
        var found = await FindAsync(userId, contractId, attachmentId, ct).ConfigureAwait(false);
        if (found.Error is { } error) return error;

        try
        {
            var download = await files.OpenDownloadAsync(found.Value!.FileId, FileAccessScope.Internal, ct).ConfigureAwait(false);
            if (download is null) return PmsError.NotFound("檔案不存在或已被清除。");
            if (download.Content is null)
            {
                logger.LogWarning("[PMS] 合約附件 {AttachmentId} 的 FMS 分類回傳導向網址，已拒絕。", attachmentId);
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

    /// <summary>虛擬刪除附件：僅業務／合約 PM；用印正本鎖定不可刪除。</summary>
    public async Task<PmsResult<bool>> DeleteAsync(Guid userId, string actor, Guid contractId, Guid attachmentId, CancellationToken ct)
    {
        var ctx = await LoadAsync(userId, contractId, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!;

        if (!ContractRules.CanManage(userId, Members(c))) return PmsError.Forbidden("僅合約的業務或合約 PM 可刪除附件。");

        var attachment = await db.ContractAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId && a.ContractId == contractId && !a.IsDeleted, ct).ConfigureAwait(false);
        if (attachment is null) return PmsError.NotFound("附件不存在。");
        if (attachment.Kind == ContractAttachmentKind.Signed) return PmsError.Conflict("PMS.CONTENT_LOCKED", "用印正本為證據文件，不可刪除。");

        var now = clock.GetUtcNow();
        attachment.IsDeleted = true;
        attachment.UpdatedBy = actor;
        attachment.UpdatedAtUtc = now;
        db.ContractEvents.Add(new ContractEvent
        {
            Id = Guid.NewGuid(), ContractId = contractId, Type = ContractEventType.AttachmentRemoved,
            Message = $"刪除附件 {attachment.OriginalFileName}", ActorUserId = userId, ActorName = actor, At = now
        });
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        try
        {
            await files.DeleteAsync(attachment.FileId, FileAccessScope.Internal, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "[PMS] 合約附件 {AttachmentId} 已標記刪除，但 FMS 刪除檔案 {FileId} 失敗。", attachmentId, attachment.FileId);
        }

        return PmsResult<bool>.Success(true);
    }

    // ---------- 內部輔助 ----------

    private static IReadOnlyList<(Guid UserId, ContractMemberRole Role)> Members(Contract c) =>
        c.Members.Select(m => (m.UserId, m.Role)).ToList();

    private async Task<PmsResult<Contract>> LoadAsync(Guid userId, Guid contractId, CancellationToken ct)
    {
        var contract = await db.Contracts.AsNoTracking().Include(c => c.Members).FirstOrDefaultAsync(c => c.Id == contractId, ct).ConfigureAwait(false);
        return contract is null || !ContractRules.IsMember(userId, Members(contract)) ? PmsError.NotFound("合約不存在。") : PmsResult<Contract>.Success(contract);
    }

    private async Task<PmsResult<ContractAttachment>> FindAsync(Guid userId, Guid contractId, Guid attachmentId, CancellationToken ct)
    {
        var ctx = await LoadAsync(userId, contractId, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;

        var attachment = await db.ContractAttachments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == attachmentId && a.ContractId == contractId && !a.IsDeleted, ct).ConfigureAwait(false);
        return attachment is null ? PmsError.NotFound("附件不存在。") : PmsResult<ContractAttachment>.Success(attachment);
    }

    private async Task CompensateAsync(Guid fileId)
    {
        try
        {
            await files.DeleteAsync(fileId, FileAccessScope.Internal, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[PMS] 合約附件中繼資料寫入失敗，且回收 FMS 檔案 {FileId} 失敗，請維運清理孤兒檔案。", fileId);
        }
    }

    private DateOnly TaipeiToday() => DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(SlaCalculator.Offset).DateTime);

    /// <summary>系統檔名：<c>YYYYMMDD_合約編號_類型_說明.ext</c>；說明移除檔案系統禁字與底線、限 60 字。</summary>
    public static string GenerateName(DateOnly date, string contractNo, ContractAttachmentKind kind, string? description, string? extension)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder();
        foreach (var ch in description ?? string.Empty)
        {
            if (invalid.Contains(ch) || ch == '_' || char.IsControl(ch)) continue;
            sb.Append(char.IsWhiteSpace(ch) ? '-' : ch);
        }

        var clean = sb.ToString().Trim('-', '.', ' ');
        if (clean.Length > 60) clean = clean[..60];
        if (clean.Length == 0) clean = "附件";

        var ext = string.IsNullOrWhiteSpace(extension) ? string.Empty : extension.Trim().ToLowerInvariant();
        if (ext.Length > 10 || ext.Any(ch => !(char.IsAsciiLetterOrDigit(ch) || ch == '.'))) ext = string.Empty;

        return $"{date:yyyyMMdd}_{contractNo}_{KindName(kind)}_{clean}{ext}";
    }

    public static string KindName(ContractAttachmentKind kind) => kind switch
    {
        ContractAttachmentKind.Draft => "合約草稿",
        ContractAttachmentKind.Signed => "用印正本",
        _ => "其他"
    };

    internal static ContractAttachmentDto ToDto(ContractAttachment a) => new(
        a.Id, a.FileName, a.OriginalFileName, a.ContentType, a.FileSize, a.Kind,
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
