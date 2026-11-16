using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGS.Modules.QTN.Application.Contracts;
using SGS.Modules.QTN.Application.Rules;
using SGS.Modules.QTN.Infrastructure.Dbcontexts;
using SGS.Modules.QTN.Infrastructure.Entities;
using SGSFramework.Core.FileManagement;

namespace SGS.Modules.QTN.Application.Services;

/// <summary>
/// 報價附件 × 檔案管理（FMS，分類 <c>qtn-quotation</c>，加密儲存、僅經 QTN 授權後存取）：需求資料、折扣說明書、客戶簽回文件。
/// 商機成員可上傳與檢視（檢視加浮水印）；客戶簽回文件為證據，不可刪除；非成員一律 404。
/// </summary>
public sealed class QuoteAttachmentService(
    QTNDbContext db,
    QtnAccess access,
    IFileManagementService files,
    TimeProvider clock,
    ILogger<QuoteAttachmentService> logger)
{
    public const string FileCategory = "qtn-quotation";
    public const string ReferenceType = "QtnQuote";
    public const int MaxAttachmentsPerQuote = 30;

    public async Task<QtnResult<QuoteAttachmentDto>> UploadAsync(
        QtnCaller caller, Guid quoteId, QuoteAttachmentKind kind, Stream content, string originalFileName, string? expectedSha256, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(content);

        var q = await LoadAsync(caller, quoteId, ct).ConfigureAwait(false);
        if (q.Error is { } error) return error;
        var quote = q.Value!;

        if (!quote.Opportunity!.Members.Any(m => m.UserId == caller.UserId)) return QtnError.Forbidden("僅商機成員可上傳附件。");
        if (quote.Status == QuoteStatus.Superseded) return QtnError.Conflict("QTN.INVALID_TRANSITION", "已被取代的版本不可再上傳附件。");
        if (!Enum.IsDefined(kind)) return QtnError.Validation("附件類型不正確。");
        if (string.IsNullOrWhiteSpace(originalFileName)) return QtnError.Validation("請選擇要上傳的檔案。");

        var count = await db.QuoteAttachments.CountAsync(a => a.QuoteVersionId == quoteId && !a.IsDeleted, ct).ConfigureAwait(false);
        if (count >= MaxAttachmentsPerQuote) return QtnError.Validation($"每個報價版本最多 {MaxAttachmentsPerQuote} 個附件。");

        var clean = Path.GetFileName(originalFileName.Trim());
        var systemName = GenerateName(Workdays.TaipeiToday(clock.GetUtcNow()), $"{quote.QuoteNo}-{quote.VersionLabel}", kind,
            Path.GetFileNameWithoutExtension(clean), Path.GetExtension(clean));

        FileMetadataDto stored;
        try
        {
            stored = await files.UploadAsync(
                new FileUploadRequest(content, systemName, FileCategory, ReferenceType, quoteId.ToString(), expectedSha256), FileAccessScope.Internal, ct)
                .ConfigureAwait(false);
        }
        catch (FileRejectedException ex)
        {
            return MapRejected(ex);
        }

        var now = clock.GetUtcNow();
        var attachment = new QuoteAttachment
        {
            Id = Guid.NewGuid(), QuoteVersionId = quoteId, FileId = stored.Id, Kind = kind, FileName = systemName, OriginalFileName = clean,
            ContentType = stored.ContentType, FileSize = stored.FileSize, Sha256 = stored.Sha256, CreatedBy = caller.Name, CreatedAtUtc = now
        };

        try
        {
            db.QuoteAttachments.Add(attachment);
            db.ApprovalLogs.Add(Log(quote, QuoteLogAction.AttachmentAdded, caller, $"上傳附件（{KindName(kind)}）{clean}"));
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            db.ChangeTracker.Clear();
            await CompensateAsync(stored.Id).ConfigureAwait(false);
            throw;
        }

        return QtnResult<QuoteAttachmentDto>.Success(ToDto(attachment));
    }

    public async Task<QtnResult<QuoteFileView>> OpenViewAsync(QtnCaller caller, Guid quoteId, Guid attachmentId, string watermark, CancellationToken ct)
    {
        var found = await FindAsync(caller, quoteId, attachmentId, ct).ConfigureAwait(false);
        if (found.Error is { } error) return error;

        try
        {
            var view = await files.OpenWatermarkedViewAsync(found.Value!.FileId, watermark, FileAccessScope.Internal, ct).ConfigureAwait(false);
            return view is null ? QtnError.NotFound("檔案不存在或已被清除。") : QtnResult<QuoteFileView>.Success(new QuoteFileView(view.Content, view.ContentType));
        }
        catch (FileRejectedException ex)
        {
            return MapRejected(ex);
        }
    }

    public async Task<QtnResult<FileDownload>> OpenDownloadAsync(QtnCaller caller, Guid quoteId, Guid attachmentId, CancellationToken ct)
    {
        var found = await FindAsync(caller, quoteId, attachmentId, ct).ConfigureAwait(false);
        if (found.Error is { } error) return error;

        try
        {
            var download = await files.OpenDownloadAsync(found.Value!.FileId, FileAccessScope.Internal, ct).ConfigureAwait(false);
            if (download is null) return QtnError.NotFound("檔案不存在或已被清除。");
            if (download.Content is null)
            {
                logger.LogWarning("[QTN] 報價附件 {AttachmentId} 的 FMS 分類回傳導向網址，已拒絕。", attachmentId);
                return QtnError.Conflict("QTN.FILE_REJECTED", "此檔案不支援經報價模組下載，請聯絡管理員檢查 FMS 分類設定。");
            }

            return QtnResult<FileDownload>.Success(download);
        }
        catch (FileRejectedException ex)
        {
            return MapRejected(ex);
        }
    }

    public async Task<QtnResult<bool>> DeleteAsync(QtnCaller caller, Guid quoteId, Guid attachmentId, CancellationToken ct)
    {
        var q = await LoadAsync(caller, quoteId, ct).ConfigureAwait(false);
        if (q.Error is { } error) return error;
        var quote = q.Value!;

        var attachment = await db.QuoteAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId && a.QuoteVersionId == quoteId && !a.IsDeleted, ct).ConfigureAwait(false);
        if (attachment is null) return QtnError.NotFound("附件不存在。");
        if (attachment.Kind == QuoteAttachmentKind.CustomerSigned) return QtnError.Conflict("QTN.CONTENT_LOCKED", "客戶簽回文件為證據，不可刪除。");
        if (!QtnAccess.Has(quote.Opportunity!, caller.UserId, OpportunityRole.Sales) && attachment.CreatedBy != caller.Name)
            return QtnError.Forbidden("僅業務或上傳者可刪除附件。");

        var now = clock.GetUtcNow();
        attachment.IsDeleted = true;
        attachment.UpdatedBy = caller.Name;
        attachment.UpdatedAtUtc = now;
        db.ApprovalLogs.Add(Log(quote, QuoteLogAction.AttachmentRemoved, caller, $"刪除附件 {attachment.OriginalFileName}"));
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        try
        {
            await files.DeleteAsync(attachment.FileId, FileAccessScope.Internal, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "[QTN] 報價附件 {AttachmentId} 已標記刪除，但 FMS 刪除檔案 {FileId} 失敗。", attachmentId, attachment.FileId);
        }

        return QtnResult<bool>.Success(true);
    }

    private async Task<QtnResult<QuoteVersion>> LoadAsync(QtnCaller caller, Guid quoteId, CancellationToken ct)
    {
        var q = await db.QuoteVersions.AsNoTracking().Include(x => x.Opportunity!).ThenInclude(o => o.Members).FirstOrDefaultAsync(x => x.Id == quoteId, ct)
            .ConfigureAwait(false);
        return q is null || !await access.CanViewAsync(q.Opportunity!, caller.UserId, ct).ConfigureAwait(false)
            ? QtnError.NotFound("報價不存在。")
            : QtnResult<QuoteVersion>.Success(q);
    }

    private async Task<QtnResult<QuoteAttachment>> FindAsync(QtnCaller caller, Guid quoteId, Guid attachmentId, CancellationToken ct)
    {
        var q = await LoadAsync(caller, quoteId, ct).ConfigureAwait(false);
        if (q.Error is { } error) return error;
        var a = await db.QuoteAttachments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == attachmentId && x.QuoteVersionId == quoteId && !x.IsDeleted, ct)
            .ConfigureAwait(false);
        return a is null ? QtnError.NotFound("附件不存在。") : QtnResult<QuoteAttachment>.Success(a);
    }

    private ApprovalLog Log(QuoteVersion q, QuoteLogAction action, QtnCaller caller, string message) => new()
    {
        Id = Guid.NewGuid(), QuoteVersionId = q.Id, Action = action, Cycle = q.ApprovalCycle, ActorUserId = caller.UserId, ActorName = caller.Name,
        IpAddress = caller.IpAddress, At = clock.GetUtcNow(), Message = message, ContentHash = q.ContentHash
    };

    private async Task CompensateAsync(Guid fileId)
    {
        try
        {
            await files.DeleteAsync(fileId, FileAccessScope.Internal, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[QTN] 報價附件中繼資料寫入失敗，且回收 FMS 檔案 {FileId} 失敗，請維運清理孤兒檔案。", fileId);
        }
    }

    /// <summary>系統檔名：<c>YYYYMMDD_報價單號-版本_類型_說明.ext</c>。</summary>
    public static string GenerateName(DateOnly date, string quoteRef, QuoteAttachmentKind kind, string? description, string? extension)
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

        return $"{date:yyyyMMdd}_{quoteRef}_{KindName(kind)}_{clean}{ext}";
    }

    public static string KindName(QuoteAttachmentKind kind) => kind switch
    {
        QuoteAttachmentKind.Requirement => "需求資料",
        QuoteAttachmentKind.DiscountExplanation => "折扣說明書",
        QuoteAttachmentKind.CustomerSigned => "客戶簽回",
        _ => "其他"
    };

    internal static QuoteAttachmentDto ToDto(QuoteAttachment a) => new(
        a.Id, a.Kind, a.FileName, a.OriginalFileName, a.ContentType, a.FileSize,
        a.ContentType is { } ct && (ct.StartsWith("image/", StringComparison.OrdinalIgnoreCase) || ct.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)),
        a.CreatedBy, a.CreatedAtUtc);

    private static QtnError MapRejected(FileRejectedException ex) => ex.StatusCode switch
    {
        503 => new QtnError("QTN.FILE_SERVICE_UNAVAILABLE", 503, "Service Unavailable", ex.Message),
        413 => new QtnError("QTN.FILE_REJECTED", 413, "Payload Too Large", ex.Message),
        415 => new QtnError("QTN.FILE_REJECTED", 415, "Unsupported Media Type", ex.Message),
        403 => new QtnError("QTN.FILE_REJECTED", 403, "Forbidden", ex.Message),
        _ => new QtnError("QTN.FILE_REJECTED", 400, "Invalid Request", ex.Message)
    };
}
