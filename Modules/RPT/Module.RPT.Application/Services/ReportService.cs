using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SGS.Modules.RPT.Application.Dtos;
using SGS.Modules.RPT.Application.Pdf;
using SGS.Modules.RPT.Infrastructure.Dbcontexts;
using SGS.Modules.RPT.Infrastructure.Domain;
using SGS.Modules.RPT.Infrastructure.Entities;
using SGSFramework.AuthTokenBucket.Services;
using SGSFramework.Core.Abstractions.Adapters;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Abstractions.Entities.ReportCategories;
using SGSFramework.Core.Abstractions.Permissions.Entities;
using SGSFramework.Core.Abstractions.ReportAccess;
using SGSFramework.Core.FileStorages;
using SGSFramework.Core.FileManagement;
using SGSFramework.ReportEngine.Abstractions;

namespace SGS.Modules.RPT.Application.Services;

/// <summary>
/// 檢驗報告生命週期服務：建立、數據登錄、指派、送審 / 審核 / 簽署、作廢重發、下載與公開真偽查驗。
/// 每項操作皆經 <see cref="IReportAccessGuard"/> 以「類別 × 實驗室 × 密級 × 狀態 × 指派 × 簽署資格」判定；
/// 無瀏覽權限者一律視為查無報告（404），避免洩漏報告存在性。
/// </summary>
public interface IReportService
{
    Task<IReadOnlyList<ReportListItemDto>> ListAsync(ReportListQuery query, CancellationToken cancellationToken = default);

    Task<ReportDetailDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ReportOpResult> CreateAsync(CreateReportRequest request, CancellationToken cancellationToken = default);

    Task<ReportOpResult> UpdateHeaderAsync(Guid id, UpdateReportHeaderRequest request, CancellationToken cancellationToken = default);

    Task<ReportOpResult> SaveResultsAsync(Guid id, SaveReportResultsRequest request, CancellationToken cancellationToken = default);

    Task<ReportOpResult> SetAssignmentsAsync(Guid id, SetReportAssignmentsRequest request, CancellationToken cancellationToken = default);

    Task<ReportOpResult> TransitionAsync(Guid id, ReportTransitionRequest request, CancellationToken cancellationToken = default);

    Task<ReportOpResult> RequestVoidAsync(Guid id, RequestReportVoidRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReportVoidRequestDto>> ListVoidRequestsAsync(bool pendingOnly, CancellationToken cancellationToken = default);

    Task<ReportOpResult> ApproveVoidAsync(Guid requestId, string? comment, CancellationToken cancellationToken = default);

    Task<ReportOpResult> RejectVoidAsync(Guid requestId, string? comment, CancellationToken cancellationToken = default);

    Task<(ReportOpResult Result, ReportFileResult? File)> DownloadAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(ReportOpResult Result, ReportFileResult? File)> PreviewAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReportStatusHistoryDto>?> GetHistoryAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(ReportOpResult Result, IReadOnlyList<ReportAccessLogDto> Logs)> GetAccessLogsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ReportVerificationDto?> VerifyAsync(string token, byte[]? fileContent, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReportUserOptionDto>> SearchAssignableUsersAsync(string? keyword, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReportCategoryOptionDto>> GetCreatableCategoriesAsync(CancellationToken cancellationToken = default);
}

public sealed class ReportService(
    RPTDbContext dbContext,
    IReportAccessGuard guard,
    IReportCategoryManagementService categoryService,
    IOrganizationIntegrationService orgIntegrationService,
    UserManager<ApplicationUser> userManager,
    IFileManagementService fileManagement,
    IFileStorageHelper<Report> legacyFileStorage,
    ReportAccessLogger accessLogger,
    IReportWorkflowNotifier notifier,
    IConfiguration configuration,
    TimeProvider timeProvider,
    ILogger<ReportService> logger) : IReportService
{
    private const int ListLimit = 500;

    /// <summary>檔案管理分類（FileManagement:Categories:Report，須設 AllowGenericApi = false）</summary>
    internal const string ReportFileCategory = "Report";

    private static readonly ReportActions[] DetailActions =
    [
        ReportActions.Edit, ReportActions.Review, ReportActions.Sign, ReportActions.Void,
        ReportActions.Download, ReportActions.ViewAudit, ReportActions.ViewBlindIdentity
    ];

    // ===== 查詢 =====

    public async Task<IReadOnlyList<ReportListItemDto>> ListAsync(ReportListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (guard.CurrentLabId is not { } labId || guard.CurrentUserId is not { } userId) return [];
        var subject = await guard.GetSubjectAsync(labId, cancellationToken).ConfigureAwait(false);
        if (subject is null) return [];

        var reports = dbContext.Reports.AsNoTracking().Include(r => r.Assignments).Where(r => r.TenantLabId == labId);
        if (query.Status.HasValue) reports = reports.Where(r => r.Status == query.Status.Value);
        if (!string.IsNullOrWhiteSpace(query.CategoryCode)) reports = reports.Where(r => r.CategoryCode == query.CategoryCode);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            reports = reports.Where(r => r.ReportNo.Contains(keyword) || r.SampleNo.Contains(keyword) || r.Title.Contains(keyword));
        }

        // 資料庫層先以授權快照粗篩（類別 + 範圍），再於記憶體以完整政策逐筆判定
        if (!subject.IsSuperAdmin)
        {
            var viewGrants = subject.Grants.Where(g => g.Actions.HasFlag(ReportActions.View)).ToList();
            var viewCategories = viewGrants.Select(g => g.CategoryCode).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var broadCategories = viewGrants
                .Where(g => g.DataScope == ReportDataScope.Global || (g.DataScope == ReportDataScope.Lab && subject.IsLabMember))
                .Select(g => g.CategoryCode).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            reports = reports.Where(r => viewCategories.Contains(r.CategoryCode) &&
                                         (broadCategories.Contains(r.CategoryCode) || r.AuthorId == userId || r.Assignments.Any(a => a.UserId == userId)));
        }

        var rows = await reports.OrderByDescending(r => r.CreatedAtUtc).Take(ListLimit)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var result = new List<ReportListItemDto>();
        foreach (var report in rows)
        {
            var context = ReportAccessGuard.ToContext(report);
            var view = ReportAccessPolicy.Evaluate(subject, context, ReportActions.View);
            if (!view.IsAllowed) continue;

            bool include = query.Box switch
            {
                ReportListBox.MyWork => context.AssignedUserIds.Contains(userId),
                ReportListBox.AwaitingReview => report.Status == ReportLifecycleStatus.InReview &&
                                               ReportAccessPolicy.Evaluate(subject, context, ReportActions.Review).IsAllowed,
                ReportListBox.AwaitingSign => report.Status == ReportLifecycleStatus.Reviewed &&
                                             ReportAccessPolicy.Evaluate(subject, context, ReportActions.Sign).IsAllowed,
                _ => true
            };
            if (!include) continue;

            result.Add(new ReportListItemDto(
                report.Id, report.ReportNo, report.Version, report.CategoryCode, report.Title, report.SampleNo,
                ReportFieldMasker.Mask(report.CustomerName, view.CanSeeBlindIdentity),
                report.Confidentiality, report.IsBlind, report.Status, report.AuthorName, report.ReviewerName, report.SignerName,
                report.CreatedAtUtc, report.SignedAt));
        }

        return result;
    }

    public async Task<ReportDetailDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var report = await LoadAsync(id, tracking: false, includeResults: true, cancellationToken).ConfigureAwait(false);
        if (report is null) return null;

        var view = await guard.AuthorizeAsync(report, ReportActions.View, cancellationToken).ConfigureAwait(false);
        if (!view.IsAllowed) return null;

        var decisions = await guard.EvaluateAsync(report, DetailActions, cancellationToken).ConfigureAwait(false);
        bool Allowed(ReportActions a) => decisions[a].IsAllowed;

        var transitions = ReportStateMachine.AvailableFrom(report.Status)
            .Where(t => t != ReportTransition.Void && Allowed(ReportStateMachine.RuleFor(t).RequiredAction))
            .ToList();

        bool hasPendingVoid = await dbContext.ReportVoidRequests.AsNoTracking()
            .AnyAsync(v => v.ReportId == id && v.Status == RoleChangeRequestStatus.Pending, cancellationToken).ConfigureAwait(false);

        var canSee = view.CanSeeBlindIdentity;
        var permissions = new ReportPermissionsDto(
            CanEdit: Allowed(ReportActions.Edit),
            CanDownload: Allowed(ReportActions.Download) && (report.PdfFileId is not null || report.PdfFileLink is not null),
            CanRequestVoid: report.Status == ReportLifecycleStatus.Published && !hasPendingVoid,
            CanViewAudit: Allowed(ReportActions.ViewAudit),
            CanSeeBlindIdentity: canSee,
            AvailableTransitions: transitions);

        await accessLogger.LogReportAsync(report, ReportAccessAction.View, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new ReportDetailDto(
            report.Id, report.ReportNo, report.Version, report.SupersedesReportId,
            report.CategoryCode, await CategoryNameAsync(report.CategoryCode, cancellationToken).ConfigureAwait(false),
            report.TenantLabId, await LabNameAsync(report.TenantLabId, cancellationToken).ConfigureAwait(false),
            report.Title,
            ReportFieldMasker.Mask(report.CustomerCode, canSee),
            ReportFieldMasker.Mask(report.CustomerName, canSee),
            report.SampleNo,
            ReportFieldMasker.Mask(report.SampleDescription, canSee),
            report.Confidentiality, report.IsBlind, report.Status,
            report.AuthorId, report.AuthorName, report.SubmittedAt, report.ReviewerName, report.ReviewedAt,
            report.SignerName, report.SignerCertificateNo, report.SignedAt, report.PdfSha256, report.VerificationToken,
            report.VoidedAt, report.VoidReason, Convert.ToBase64String(report.RowVersion),
            report.Results.OrderBy(r => r.Sequence).Select(r => new ReportResultDto(
                r.Id, r.Sequence, r.ItemCode, r.ItemName, r.Value, r.Unit, r.LimitText, r.Method,
                r.ValueSource, r.InstrumentValue, r.ChangeReason, r.Remark)).ToList(),
            report.Assignments.OrderBy(a => a.Role).Select(a => new ReportAssignmentDto(a.UserId, a.UserName, a.Role)).ToList(),
            permissions);
    }

    // ===== 建立與編輯 =====

    public async Task<ReportOpResult> CreateAsync(CreateReportRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (guard.CurrentLabId is not { } labId) return ReportOpResult.Fail("請先切換至報告所屬的區域實驗室。");
        if (guard.CurrentUserId is not { } userId) return ReportOpResult.Fail("無法識別使用者身分。");
        if (string.IsNullOrWhiteSpace(request.Title)) return ReportOpResult.Fail("請填寫報告標題。");
        if (string.IsNullOrWhiteSpace(request.SampleNo)) return ReportOpResult.Fail("請填寫樣品編號。");

        var categories = await categoryService.ListCategoriesAsync(cancellationToken).ConfigureAwait(false);
        var category = categories.FirstOrDefault(c => string.Equals(c.Code, request.CategoryCode?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (category is null || !category.IsActive) return ReportOpResult.Fail("找不到指定的報告類別，或該類別已停用。");

        if (!await guard.CanCreateAsync(labId, category.Code, cancellationToken).ConfigureAwait(false))
        {
            return ReportOpResult.Fail("您不具此類別於目前實驗室之編輯授權，或此實驗室未承作該類別。");
        }

        var now = timeProvider.GetUtcNow();
        var report = new Report
        {
            ReportNo = await NextReportNoAsync(category.Code, now, cancellationToken).ConfigureAwait(false),
            CategoryCode = category.Code,
            TenantLabId = labId,
            Title = request.Title.Trim(),
            SampleNo = request.SampleNo.Trim(),
            SampleDescription = Trim(request.SampleDescription),
            CustomerCode = Trim(request.CustomerCode),
            CustomerName = Trim(request.CustomerName),
            Confidentiality = request.Confidentiality ?? category.DefaultConfidentiality,
            IsBlind = request.IsBlind,
            AuthorId = userId,
            AuthorName = guard.CurrentUserName,
            CreatedBy = guard.CurrentUserName
        };
        report.Assignments.Add(new ReportAssignment
        {
            ReportId = report.Id,
            UserId = userId,
            UserName = guard.CurrentUserName,
            Role = ReportAssignmentRole.Analyst,
            CreatedBy = guard.CurrentUserName
        });

        dbContext.Reports.Add(report);
        AddHistory(report, null, ReportLifecycleStatus.Draft, "Create", null);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("[Report] {Actor} 建立報告 {ReportNo}（{Category} / {Lab}）", guard.CurrentUserName, report.ReportNo, report.CategoryCode, labId);
        return ReportOpResult.Ok($"已建立報告草稿 {report.ReportNo}。", report.Id);
    }

    public async Task<ReportOpResult> UpdateHeaderAsync(Guid id, UpdateReportHeaderRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (report, denied) = await LoadAuthorizedAsync(id, ReportActions.Edit, includeResults: false, cancellationToken).ConfigureAwait(false);
        if (report is null) return denied!;
        if (string.IsNullOrWhiteSpace(request.Title)) return ReportOpResult.Fail("請填寫報告標題。");
        if (string.IsNullOrWhiteSpace(request.SampleNo)) return ReportOpResult.Fail("請填寫樣品編號。");

        ApplyRowVersion(report, request.RowVersion);
        report.Title = request.Title.Trim();
        report.SampleNo = request.SampleNo.Trim();
        report.SampleDescription = Trim(request.SampleDescription);
        report.CustomerCode = Trim(request.CustomerCode);
        report.CustomerName = Trim(request.CustomerName);
        report.Confidentiality = request.Confidentiality;
        report.IsBlind = request.IsBlind;

        return await SaveAsync(report, "已更新報告基本資料。", cancellationToken).ConfigureAwait(false);
    }

    public async Task<ReportOpResult> SaveResultsAsync(Guid id, SaveReportResultsRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (report, denied) = await LoadAuthorizedAsync(id, ReportActions.Edit, includeResults: true, cancellationToken).ConfigureAwait(false);
        if (report is null) return denied!;

        var items = request.Items ?? [];
        if (items.Any(i => string.IsNullOrWhiteSpace(i.ItemCode) || string.IsNullOrWhiteSpace(i.ItemName)))
        {
            return ReportOpResult.Fail("每一列檢驗項目皆須填寫項目代碼與名稱。");
        }

        var keptIds = items.Where(i => i.Id.HasValue).Select(i => i.Id!.Value).ToHashSet();
        foreach (var removed in report.Results.Where(r => !keptIds.Contains(r.Id)).ToList())
        {
            // 草稿階段刪除列仍由稽核攔截器保留原值
            dbContext.ReportResults.Remove(removed);
            report.Results.Remove(removed);
        }

        int sequence = 1;
        foreach (var item in items)
        {
            var existing = item.Id.HasValue ? report.Results.FirstOrDefault(r => r.Id == item.Id.Value) : null;
            var value = Trim(item.Value);

            if (existing is null)
            {
                var created = new ReportResult
                {
                    ReportId = report.Id,
                    ValueSource = item.ValueSource,
                    InstrumentValue = item.ValueSource == ReportValueSource.Instrument ? value : null,
                    CreatedBy = guard.CurrentUserName
                };
                ApplyResult(created, item, value, sequence++);
                dbContext.ReportResults.Add(created);
                report.Results.Add(created);
                continue;
            }

            // 儀器拋轉數據防篡改：與原始值不同時須填寫變更原因；數據來源不可由儀器改為人工
            if (existing.ValueSource == ReportValueSource.Instrument &&
                !string.Equals(value, existing.InstrumentValue, StringComparison.Ordinal) &&
                string.IsNullOrWhiteSpace(item.ChangeReason))
            {
                return ReportOpResult.Fail($"項目「{existing.ItemName}」為儀器拋轉數據，人工修改須填寫變更原因。");
            }

            ApplyResult(existing, item, value, sequence++);
        }

        // 觸發報告列更新，使並行版本（RowVersion）檢核涵蓋結果數據
        ApplyRowVersion(report, request.RowVersion);
        report.UpdatedAtUtc = timeProvider.GetUtcNow();

        return await SaveAsync(report, $"已儲存 {items.Count} 筆檢驗結果。", cancellationToken).ConfigureAwait(false);
    }

    public async Task<ReportOpResult> SetAssignmentsAsync(Guid id, SetReportAssignmentsRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (report, denied) = await LoadAuthorizedAsync(id, ReportActions.View, includeResults: false, cancellationToken).ConfigureAwait(false);
        if (report is null) return denied!;
        if (report.Status == ReportLifecycleStatus.Voided) return ReportOpResult.Fail("已作廢報告不可變更指派。");

        var target = (request.Assignments ?? []).Distinct().ToList();
        var names = new Dictionary<Guid, string>();
        foreach (var userId in target.Select(a => a.UserId).Distinct())
        {
            var user = await userManager.FindByIdAsync(userId.ToString()).ConfigureAwait(false);
            if (user is null || user.IsDeleted) return ReportOpResult.Fail($"找不到使用者（{userId}）。");
            names[userId] = string.IsNullOrWhiteSpace(user.FullName) ? user.UserName ?? userId.ToString() : user.FullName;
        }

        // 作者的分析員指派固定保留（作者身分用於職能分立判定）
        if (!target.Any(a => a.UserId == report.AuthorId && a.Role == ReportAssignmentRole.Analyst))
        {
            target.Add(new ReportAssignmentInput(report.AuthorId, ReportAssignmentRole.Analyst));
            names[report.AuthorId] = report.AuthorName;
        }

        foreach (var removed in report.Assignments.Where(a => !target.Any(t => t.UserId == a.UserId && t.Role == a.Role)).ToList())
        {
            dbContext.ReportAssignments.Remove(removed);
            report.Assignments.Remove(removed);
        }

        var added = new List<Guid>();
        foreach (var input in target.Where(t => !report.Assignments.Any(a => a.UserId == t.UserId && a.Role == t.Role)))
        {
            var assignment = new ReportAssignment
            {
                ReportId = report.Id,
                UserId = input.UserId,
                UserName = names[input.UserId],
                Role = input.Role,
                CreatedBy = guard.CurrentUserName
            };
            dbContext.ReportAssignments.Add(assignment);
            report.Assignments.Add(assignment);
            added.Add(input.UserId);
        }

        AddHistory(report, report.Status, report.Status, "Assign",
            string.Join("、", report.Assignments.Select(a => $"{a.UserName}({a.Role})")));
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await notifier.NotifyAsync(added, report, $"您已被指派處理報告 {report.ReportNo}。", cancellationToken).ConfigureAwait(false);
        return ReportOpResult.Ok("已更新報告指派。");
    }

    // ===== 生命週期 =====

    public async Task<ReportOpResult> TransitionAsync(Guid id, ReportTransitionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Transition == ReportTransition.Void) return ReportOpResult.Fail("作廢須透過作廢申請並經核准。");

        var rule = ReportStateMachine.RuleFor(request.Transition);
        var (report, denied) = await LoadAuthorizedAsync(id, rule.RequiredAction, includeResults: request.Transition is ReportTransition.Submit or ReportTransition.Sign,
            cancellationToken, request.Transition).ConfigureAwait(false);
        if (report is null) return denied!;

        var error = ReportStateMachine.Validate(report.Status, request.Transition, request.Reason);
        if (error is not null) return ReportOpResult.Fail(error);

        var userId = guard.CurrentUserId!.Value;
        var now = timeProvider.GetUtcNow();
        var from = report.Status;
        ApplyRowVersion(report, request.RowVersion);

        IEnumerable<Guid> notifyUsers = [];
        string notifyMessage = string.Empty;

        switch (request.Transition)
        {
            case ReportTransition.Submit:
                if (!report.Results.Any(r => !string.IsNullOrWhiteSpace(r.Value)))
                {
                    return ReportOpResult.Fail("送審前至少須登錄一筆檢驗結果。");
                }
                report.SubmittedAt = now;
                notifyUsers = AssignedTo(report, ReportAssignmentRole.Reviewer);
                notifyMessage = $"報告 {report.ReportNo} 已送審，待您審核。";
                break;

            case ReportTransition.ApproveReview:
                report.ReviewerId = userId;
                report.ReviewerName = guard.CurrentUserName;
                report.ReviewedAt = now;
                notifyUsers = AssignedTo(report, ReportAssignmentRole.Signer);
                notifyMessage = $"報告 {report.ReportNo} 已審核完成，待您簽署。";
                break;

            case ReportTransition.RejectReview:
            case ReportTransition.RejectSign:
                report.SubmittedAt = null;
                report.ReviewerId = null;
                report.ReviewerName = null;
                report.ReviewedAt = null;
                notifyUsers = [report.AuthorId];
                notifyMessage = $"報告 {report.ReportNo} 已被退回：{request.Reason}";
                break;

            case ReportTransition.Sign:
                var signError = await SignAsync(report, userId, now, cancellationToken).ConfigureAwait(false);
                if (signError is not null) return ReportOpResult.Fail(signError);
                notifyUsers = AssignedTo(report, ReportAssignmentRole.SalesOwner).Append(report.AuthorId);
                notifyMessage = $"報告 {report.ReportNo} 已簽署發行。";
                break;
        }

        report.Status = rule.To;
        AddHistory(report, from, rule.To, request.Transition.ToString(), Trim(request.Reason));

        ReportOpResult saved;
        try
        {
            saved = await SaveAsync(report, $"報告 {report.ReportNo} 已{ReportStateMachine.TransitionLabel(request.Transition)}。", cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            if (request.Transition == ReportTransition.Sign) await DiscardSignedPdfAsync(report).ConfigureAwait(false);
            throw;
        }

        if (!saved.Succeeded && request.Transition == ReportTransition.Sign)
        {
            await DiscardSignedPdfAsync(report).ConfigureAwait(false);
        }

        if (saved.Succeeded)
        {
            logger.LogInformation("[Report] {Actor} 對報告 {ReportNo} 執行 {Transition}：{From} → {To}",
                guard.CurrentUserName, report.ReportNo, request.Transition, from, rule.To);
            await notifier.NotifyAsync(notifyUsers.Where(u => u != userId), report, notifyMessage, cancellationToken).ConfigureAwait(false);
        }

        return saved;
    }

    /// <summary>
    /// 簽署：快照簽署人資格證號，產生正式 PDF 並保存，計算 SHA-256 與公開查驗碼。正式檔案自此不再重新產生。
    /// PDF 存於檔案管理的 Report 分類（地端機密、禁止經通用 api/v1/files 存取），下載一律經本服務授權。
    /// </summary>
    private async Task<string?> SignAsync(Report report, Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var qualifications = await categoryService.ListQualificationsAsync(report.CategoryCode, userId, includeInactive: false, cancellationToken).ConfigureAwait(false);
        var qualification = qualifications.FirstOrDefault(q => q.TenantLabId == report.TenantLabId && q.Status == LabAuditStatuses.Active);
        if (qualification is null) return "不具此類別於本實驗室之有效簽署人資格。";

        report.SignerId = userId;
        report.SignerName = guard.CurrentUserName;
        report.SignerCertificateNo = qualification.CertificateNo;
        report.SignedAt = now;
        report.VerificationToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

        var pdf = ReportPdfGenerator.Generate(await BuildPdfDataAsync(report, preview: false, watermark: null, cancellationToken).ConfigureAwait(false));

        report.PdfSha256 = Convert.ToHexStringLower(SHA256.HashData(pdf));

        try
        {
            await using var stream = new MemoryStream(pdf, writable: false);
            var file = await fileManagement.UploadAsync(
                new FileUploadRequest(stream, $"{report.ReportNo}.pdf", ReportFileCategory, nameof(Report), report.Id.ToString()),
                cancellationToken: cancellationToken).ConfigureAwait(false);

            // 檔案管理以實際落地內容計算雜湊，須與簽署時的 PDF 一致（PDF 不經重新編碼）
            if (!string.Equals(file.Sha256, report.PdfSha256, StringComparison.OrdinalIgnoreCase))
            {
                await fileManagement.DeleteAsync(file.Id, cancellationToken: CancellationToken.None).ConfigureAwait(false);
                return "正式報告檔案保存後雜湊不一致，已取消簽署。";
            }

            report.PdfFileId = file.Id;
            report.PdfFileLink = null;
        }
        catch (FileRejectedException ex)
        {
            logger.LogError(ex, "[Report] 報告 {ReportNo} 正式 PDF 保存失敗（{Status}）。", report.ReportNo, ex.StatusCode);
            return $"正式報告檔案保存失敗：{ex.Message}";
        }

        return null;
    }

    /// <summary>簽署未能寫入報告時，移除已保存的正式 PDF（虛擬刪除，由清除排程回收），避免孤兒檔案</summary>
    private async Task DiscardSignedPdfAsync(Report report)
    {
        if (report.PdfFileId is not { } fileId) return;

        try
        {
            await fileManagement.DeleteAsync(fileId, cancellationToken: CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Report] 報告 {ReportNo} 簽署失敗後無法移除正式 PDF {FileId}，需人工清理。", report.ReportNo, fileId);
        }
    }

    // ===== 作廢與重發（Maker-Checker） =====

    public async Task<ReportOpResult> RequestVoidAsync(Guid id, RequestReportVoidRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (report, denied) = await LoadAuthorizedAsync(id, ReportActions.View, includeResults: false, cancellationToken).ConfigureAwait(false);
        if (report is null) return denied!;
        if (report.Status != ReportLifecycleStatus.Published) return ReportOpResult.Fail("僅已發行報告可申請作廢。");
        if (string.IsNullOrWhiteSpace(request.Reason)) return ReportOpResult.Fail("請填寫作廢原因。");

        bool pending = await dbContext.ReportVoidRequests
            .AnyAsync(v => v.ReportId == id && v.Status == RoleChangeRequestStatus.Pending, cancellationToken).ConfigureAwait(false);
        if (pending) return ReportOpResult.Fail("此報告已有待審核的作廢申請。");

        var entity = new ReportVoidRequest
        {
            ReportId = report.Id,
            Reason = request.Reason.Trim(),
            Reissue = request.Reissue,
            RequestedBy = guard.CurrentUserId!.Value,
            RequestedByName = guard.CurrentUserName,
            RequestedAt = timeProvider.GetUtcNow(),
            CreatedBy = guard.CurrentUserName
        };
        dbContext.ReportVoidRequests.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("[Report] {Actor} 申請作廢報告 {ReportNo}（重發：{Reissue}）", guard.CurrentUserName, report.ReportNo, request.Reissue);
        return ReportOpResult.Ok("已送出作廢申請，待實驗室主管核准。", entity.Id);
    }

    public async Task<IReadOnlyList<ReportVoidRequestDto>> ListVoidRequestsAsync(bool pendingOnly, CancellationToken cancellationToken = default)
    {
        var query = dbContext.ReportVoidRequests.AsNoTracking();
        if (pendingOnly) query = query.Where(v => v.Status == RoleChangeRequestStatus.Pending);
        var requests = await query.OrderByDescending(v => v.RequestedAt).Take(200).ToListAsync(cancellationToken).ConfigureAwait(false);

        var reportIds = requests.Select(r => r.ReportId).Distinct().ToList();
        var reports = await dbContext.Reports.AsNoTracking().Include(r => r.Assignments)
            .Where(r => reportIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, cancellationToken).ConfigureAwait(false);

        var userId = guard.CurrentUserId;
        var result = new List<ReportVoidRequestDto>();
        foreach (var request in requests)
        {
            if (!reports.TryGetValue(request.ReportId, out var report)) continue;

            var decisions = await guard.EvaluateAsync(report, [ReportActions.View, ReportActions.Void], cancellationToken).ConfigureAwait(false);
            if (!decisions[ReportActions.View].IsAllowed) continue;

            bool canApprove = request.Status == RoleChangeRequestStatus.Pending &&
                              decisions[ReportActions.Void].IsAllowed && request.RequestedBy != userId;
            result.Add(new ReportVoidRequestDto(
                request.Id, report.Id, report.ReportNo, report.CategoryCode, request.Reason, request.Reissue, request.Status,
                request.RequestedByName, request.RequestedAt, request.ReviewedByName, request.ReviewedAt, request.ReviewComment,
                request.ReissuedReportId, canApprove, request.RequestedBy == userId));
        }

        return result;
    }

    public async Task<ReportOpResult> ApproveVoidAsync(Guid requestId, string? comment, CancellationToken cancellationToken = default)
    {
        var request = await dbContext.ReportVoidRequests.FirstOrDefaultAsync(v => v.Id == requestId, cancellationToken).ConfigureAwait(false);
        if (request is null) return ReportOpResult.Fail("找不到指定的作廢申請。");
        if (request.Status != RoleChangeRequestStatus.Pending) return ReportOpResult.Fail("此申請已處理完畢。");
        if (request.RequestedBy == guard.CurrentUserId) return ReportOpResult.Fail("雙重審批規定：申請人不得核准自己的作廢申請。");

        var (report, denied) = await LoadAuthorizedAsync(request.ReportId, ReportActions.Void, includeResults: true, cancellationToken).ConfigureAwait(false);
        if (report is null) return denied!;

        var now = timeProvider.GetUtcNow();
        report.Status = ReportLifecycleStatus.Voided;
        report.VoidedAt = now;
        report.VoidReason = request.Reason;
        AddHistory(report, ReportLifecycleStatus.Published, ReportLifecycleStatus.Voided, "Void", request.Reason);

        request.Status = RoleChangeRequestStatus.Approved;
        request.ReviewedBy = guard.CurrentUserId;
        request.ReviewedByName = guard.CurrentUserName;
        request.ReviewedAt = now;
        request.ReviewComment = Trim(comment);

        Report? reissued = null;
        if (request.Reissue)
        {
            reissued = CreateReissue(report);
            request.ReissuedReportId = reissued.Id;
        }

        var saved = await SaveAsync(report, reissued is null
            ? $"報告 {report.ReportNo} 已作廢。"
            : $"報告 {report.ReportNo} 已作廢，並建立重發草稿 {reissued.ReportNo}。", cancellationToken).ConfigureAwait(false);

        if (saved.Succeeded)
        {
            logger.LogInformation("[Report] {Actor} 核准作廢報告 {ReportNo}（申請人 {Requester}）", guard.CurrentUserName, report.ReportNo, request.RequestedByName);
            await notifier.NotifyAsync([report.AuthorId, request.RequestedBy], report,
                reissued is null ? $"報告 {report.ReportNo} 已作廢。" : $"報告 {report.ReportNo} 已作廢，請於重發草稿 {reissued.ReportNo} 修正。",
                cancellationToken).ConfigureAwait(false);
            return saved with { EntityId = reissued?.Id };
        }

        return saved;
    }

    public async Task<ReportOpResult> RejectVoidAsync(Guid requestId, string? comment, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(comment)) return ReportOpResult.Fail("請填寫駁回原因。");

        var request = await dbContext.ReportVoidRequests.FirstOrDefaultAsync(v => v.Id == requestId, cancellationToken).ConfigureAwait(false);
        if (request is null) return ReportOpResult.Fail("找不到指定的作廢申請。");
        if (request.Status != RoleChangeRequestStatus.Pending) return ReportOpResult.Fail("此申請已處理完畢。");

        var (report, denied) = await LoadAuthorizedAsync(request.ReportId, ReportActions.Void, includeResults: false, cancellationToken).ConfigureAwait(false);
        if (report is null) return denied!;

        request.Status = RoleChangeRequestStatus.Rejected;
        request.ReviewedBy = guard.CurrentUserId;
        request.ReviewedByName = guard.CurrentUserName;
        request.ReviewedAt = timeProvider.GetUtcNow();
        request.ReviewComment = comment.Trim();
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await notifier.NotifyAsync([request.RequestedBy], report, $"報告 {report.ReportNo} 的作廢申請已被駁回：{request.ReviewComment}", cancellationToken).ConfigureAwait(false);
        return ReportOpResult.Ok("已駁回作廢申請。");
    }

    /// <summary>重發：複製表頭與結果為新版草稿（版本 +1，取代前一版），由原作者修正後重新走審核與簽署</summary>
    private Report CreateReissue(Report original)
    {
        var baseNo = original.SupersedesReportId is null ? original.ReportNo : original.ReportNo[..original.ReportNo.LastIndexOf("-R", StringComparison.Ordinal)];
        var reissue = new Report
        {
            ReportNo = $"{baseNo}-R{original.Version}",
            Version = original.Version + 1,
            SupersedesReportId = original.Id,
            CategoryCode = original.CategoryCode,
            TenantLabId = original.TenantLabId,
            Title = original.Title,
            CustomerCode = original.CustomerCode,
            CustomerName = original.CustomerName,
            SampleNo = original.SampleNo,
            SampleDescription = original.SampleDescription,
            Confidentiality = original.Confidentiality,
            IsBlind = original.IsBlind,
            AuthorId = original.AuthorId,
            AuthorName = original.AuthorName,
            CreatedBy = guard.CurrentUserName
        };

        foreach (var assignment in original.Assignments)
        {
            reissue.Assignments.Add(new ReportAssignment
            {
                ReportId = reissue.Id,
                UserId = assignment.UserId,
                UserName = assignment.UserName,
                Role = assignment.Role,
                CreatedBy = guard.CurrentUserName
            });
        }

        foreach (var result in original.Results.OrderBy(r => r.Sequence))
        {
            reissue.Results.Add(new ReportResult
            {
                ReportId = reissue.Id,
                Sequence = result.Sequence,
                ItemCode = result.ItemCode,
                ItemName = result.ItemName,
                Value = result.Value,
                Unit = result.Unit,
                LimitText = result.LimitText,
                Method = result.Method,
                ValueSource = result.ValueSource,
                InstrumentValue = result.InstrumentValue,
                ChangeReason = result.ChangeReason,
                Remark = result.Remark,
                CreatedBy = guard.CurrentUserName
            });
        }

        dbContext.Reports.Add(reissue);
        AddHistory(reissue, null, ReportLifecycleStatus.Draft, "Reissue", $"重發自 {original.ReportNo}：{original.VoidReason}");
        return reissue;
    }

    // ===== 檔案 =====

    public async Task<(ReportOpResult Result, ReportFileResult? File)> DownloadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var (report, denied) = await LoadAuthorizedAsync(id, ReportActions.Download, includeResults: false, cancellationToken).ConfigureAwait(false);
        if (report is null) return (denied!, null);
        if (report.PdfFileId is null && string.IsNullOrWhiteSpace(report.PdfFileLink)) return (ReportOpResult.Fail("此報告尚無正式 PDF。"), null);

        Stream? content;
        try
        {
            content = await OpenSignedPdfAsync(report, cancellationToken).ConfigureAwait(false);
        }
        catch (FileRejectedException ex)
        {
            logger.LogError(ex, "[Report] 報告 {ReportNo} 正式 PDF 無法讀取（{Status}）。", report.ReportNo, ex.StatusCode);
            return (ReportOpResult.Fail("正式報告檔案暫時無法讀取，請聯絡系統管理員。"), null);
        }

        if (content is null) return (ReportOpResult.Fail("正式報告檔案不存在，請聯絡系統管理員。"), null);

        // 完整性檢核：檔案雜湊須與簽署時保存者一致，否則拒絕提供並記錄（串流計算，不整檔讀入記憶體）
        var actualHash = Convert.ToHexStringLower(await SHA256.HashDataAsync(content, cancellationToken).ConfigureAwait(false));
        if (!string.Equals(actualHash, report.PdfSha256, StringComparison.OrdinalIgnoreCase))
        {
            await content.DisposeAsync().ConfigureAwait(false);
            logger.LogCritical("[Report] 報告 {ReportNo} 正式檔案雜湊不符，疑遭竄改：檔案 {FileId} / 舊版連結 {Link}",
                report.ReportNo, report.PdfFileId, report.PdfFileLink);
            return (ReportOpResult.Fail("正式報告檔案完整性檢核失敗，已通報系統管理員。"), null);
        }

        content.Position = 0;
        await accessLogger.LogReportAsync(report, ReportAccessAction.Download, cancellationToken: cancellationToken).ConfigureAwait(false);
        return (ReportOpResult.Ok("OK"), new ReportFileResult(content, $"{report.ReportNo}.pdf"));
    }

    /// <summary>
    /// 開啟正式 PDF：改接檔案管理後以 PdfFileId 讀取；之前簽署的報告仍以舊版 PdfFileLink（LocalFileStorageHelper）讀取。
    /// 不存在時回傳 null。
    /// </summary>
    private async Task<Stream?> OpenSignedPdfAsync(Report report, CancellationToken cancellationToken)
    {
        if (report.PdfFileId is { } fileId)
        {
            var download = await fileManagement.OpenDownloadAsync(fileId, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (download?.Content is null) return null;
            if (download.Content.CanSeek) return download.Content;

            // 雜湊驗證後需倒回開頭，不可定位的串流（未來的物件儲存體）先緩衝
            var buffer = new MemoryStream();
            await using (download.Content.ConfigureAwait(false))
            {
                await download.Content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            }

            buffer.Position = 0;
            return buffer;
        }

        var path = legacyFileStorage.GetPhysicalPath(report.PdfFileLink!);
        return File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, useAsync: true)
            : null;
    }

    public async Task<(ReportOpResult Result, ReportFileResult? File)> PreviewAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var report = await LoadAsync(id, tracking: false, includeResults: true, cancellationToken).ConfigureAwait(false);
        if (report is null) return (ReportOpResult.Missing(), null);

        var view = await guard.AuthorizeAsync(report, ReportActions.View, cancellationToken).ConfigureAwait(false);
        if (!view.IsAllowed) return (ReportOpResult.Missing(), null);

        var watermark = $"預覽 · {guard.CurrentUserName} · {timeProvider.GetLocalNow():yyyy-MM-dd HH:mm}";
        var data = await BuildPdfDataAsync(report, preview: true, watermark, cancellationToken).ConfigureAwait(false);
        data = data with
        {
            CustomerName = ReportFieldMasker.Mask(data.CustomerName, view.CanSeeBlindIdentity),
            SampleDescription = ReportFieldMasker.Mask(data.SampleDescription, view.CanSeeBlindIdentity)
        };

        var content = ReportPdfGenerator.Generate(data);
        await accessLogger.LogReportAsync(report, ReportAccessAction.Print, "preview", cancellationToken).ConfigureAwait(false);
        return (ReportOpResult.Ok("OK"), new ReportFileResult(new MemoryStream(content, writable: false), $"{report.ReportNo}_preview.pdf"));
    }

    // ===== 軌跡 =====

    public async Task<IReadOnlyList<ReportStatusHistoryDto>?> GetHistoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var (report, _) = await LoadAuthorizedAsync(id, ReportActions.View, includeResults: false, cancellationToken, tracking: false).ConfigureAwait(false);
        if (report is null) return null;

        return await dbContext.ReportStatusHistories.AsNoTracking()
            .Where(h => h.ReportId == id)
            .OrderBy(h => h.OccurredAt)
            .Select(h => new ReportStatusHistoryDto(h.FromStatus, h.ToStatus, h.Action, h.ActorName, h.Reason, h.OccurredAt))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<(ReportOpResult Result, IReadOnlyList<ReportAccessLogDto> Logs)> GetAccessLogsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var (report, denied) = await LoadAuthorizedAsync(id, ReportActions.ViewAudit, includeResults: false, cancellationToken, tracking: false).ConfigureAwait(false);
        if (report is null) return (denied!, []);

        var logs = await dbContext.ReportAccessLogs.AsNoTracking()
            .Where(l => l.ReportId == id)
            .OrderByDescending(l => l.OccurredAt).Take(500)
            .Select(l => new ReportAccessLogDto(l.Action, l.UserName, l.RemoteIp, l.Detail, l.OccurredAt))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return (ReportOpResult.Ok("OK"), logs);
    }

    // ===== 公開真偽查驗 =====

    public async Task<ReportVerificationDto?> VerifyAsync(string token, byte[]? fileContent, CancellationToken cancellationToken = default)
    {
        var normalized = token?.Trim().ToUpperInvariant() ?? string.Empty;
        if (normalized.Length != 32 || !normalized.All(Uri.IsHexDigit)) return null;

        var report = await dbContext.Reports.AsNoTracking()
            .FirstOrDefaultAsync(r => r.VerificationToken == normalized, cancellationToken).ConfigureAwait(false);
        if (report is null) return null;

        bool isSuperseded = await dbContext.Reports.AsNoTracking()
            .AnyAsync(r => r.SupersedesReportId == report.Id, cancellationToken).ConfigureAwait(false);

        bool? matches = fileContent is null
            ? null
            : string.Equals(Convert.ToHexStringLower(SHA256.HashData(fileContent)), report.PdfSha256, StringComparison.OrdinalIgnoreCase);

        await accessLogger.LogReportAsync(report, ReportAccessAction.Verify,
            matches is null ? "token" : matches.Value ? "file-match" : "file-mismatch", cancellationToken).ConfigureAwait(false);

        return new ReportVerificationDto(
            report.ReportNo, report.Version, report.CategoryCode,
            await CategoryNameAsync(report.CategoryCode, cancellationToken).ConfigureAwait(false),
            await LabNameAsync(report.TenantLabId, cancellationToken).ConfigureAwait(false),
            report.Status, report.SignerName, report.SignedAt, report.PdfSha256, isSuperseded, matches);
    }

    public async Task<IReadOnlyList<ReportUserOptionDto>> SearchAssignableUsersAsync(string? keyword, CancellationToken cancellationToken = default)
    {
        var term = keyword?.Trim() ?? string.Empty;
        var users = userManager.Users.AsNoTracking().Where(u => !u.IsDeleted);
        if (term.Length > 0)
        {
            users = users.Where(u => (u.UserName != null && u.UserName.Contains(term)) || u.FullName.Contains(term) ||
                                     (u.EmployeeNo != null && u.EmployeeNo.Contains(term)));
        }

        var rows = await users.OrderBy(u => u.UserName).Take(50)
            .Select(u => new { u.Id, u.UserName, u.FullName, u.EmployeeNo })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return rows.Select(u => new ReportUserOptionDto(
            u.Id,
            string.IsNullOrWhiteSpace(u.FullName) ? u.UserName ?? u.Id.ToString() : u.FullName,
            string.IsNullOrWhiteSpace(u.EmployeeNo) ? u.UserName : $"{u.UserName}（{u.EmployeeNo}）")).ToList();
    }

    public async Task<IReadOnlyList<ReportCategoryOptionDto>> GetCreatableCategoriesAsync(CancellationToken cancellationToken = default)
    {
        if (guard.CurrentLabId is not { } labId) return [];

        var result = new List<ReportCategoryOptionDto>();
        foreach (var category in (await categoryService.ListCategoriesAsync(cancellationToken).ConfigureAwait(false)).Where(c => c.IsActive))
        {
            if (await guard.CanCreateAsync(labId, category.Code, cancellationToken).ConfigureAwait(false))
            {
                result.Add(new ReportCategoryOptionDto(category.Code, category.Name, category.DefaultConfidentiality, category.IsActive));
            }
        }

        return result;
    }

    // ===== 內部輔助 =====

    private Task<Report?> LoadAsync(Guid id, bool tracking, bool includeResults, CancellationToken cancellationToken)
    {
        IQueryable<Report> query = dbContext.Reports.Include(r => r.Assignments);
        if (includeResults) query = query.Include(r => r.Results);
        if (!tracking) query = query.AsNoTracking();
        return query.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    /// <summary>
    /// 載入並授權；無瀏覽權限回傳 404（Missing），可瀏覽但不可執行此動作則回傳原因
    /// </summary>
    private async Task<(Report? Report, ReportOpResult? Denied)> LoadAuthorizedAsync(
        Guid id, ReportActions action, bool includeResults, CancellationToken cancellationToken,
        ReportTransition? transition = null, bool tracking = true)
    {
        var report = await LoadAsync(id, tracking, includeResults, cancellationToken).ConfigureAwait(false);
        if (report is null) return (null, ReportOpResult.Missing());

        var decision = await guard.AuthorizeAsync(report, action, cancellationToken).ConfigureAwait(false);
        if (decision.IsAllowed) return (report, null);

        if (action != ReportActions.View)
        {
            var view = await guard.AuthorizeAsync(report, ReportActions.View, cancellationToken).ConfigureAwait(false);
            if (view.IsAllowed)
            {
                // 狀態不符時優先回報狀態機訊息，較授權訊息易懂
                var stateError = transition.HasValue ? ReportStateMachine.Validate(report.Status, transition.Value, "-") : null;
                return (null, ReportOpResult.Fail(stateError ?? decision.Reason));
            }
        }

        return (null, ReportOpResult.Missing());
    }

    private async Task<ReportOpResult> SaveAsync(Report report, string successMessage, CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return ReportOpResult.Ok(successMessage, report.Id);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ReportOpResult.Fail("報告已被其他人修改，請重新整理後再操作。");
        }
    }

    private void ApplyRowVersion(Report report, string? rowVersion)
    {
        if (!string.IsNullOrWhiteSpace(rowVersion))
        {
            dbContext.Entry(report).Property(r => r.RowVersion).OriginalValue = Convert.FromBase64String(rowVersion);
        }
    }

    private void AddHistory(Report report, ReportLifecycleStatus? from, ReportLifecycleStatus to, string action, string? reason)
    {
        dbContext.ReportStatusHistories.Add(new ReportStatusHistory
        {
            ReportId = report.Id,
            FromStatus = from,
            ToStatus = to,
            Action = action,
            ActorId = guard.CurrentUserId,
            ActorName = guard.CurrentUserName,
            Reason = reason is { Length: > 1000 } ? reason[..1000] : reason,
            OccurredAt = timeProvider.GetUtcNow()
        });
    }

    private static void ApplyResult(ReportResult target, SaveReportResultItem item, string? value, int sequence)
    {
        target.Sequence = sequence;
        target.ItemCode = item.ItemCode.Trim();
        target.ItemName = item.ItemName.Trim();
        target.Value = value;
        target.Unit = Trim(item.Unit);
        target.LimitText = Trim(item.LimitText);
        target.Method = Trim(item.Method);
        target.ChangeReason = Trim(item.ChangeReason);
        target.Remark = Trim(item.Remark);
    }

    private static IEnumerable<Guid> AssignedTo(Report report, ReportAssignmentRole role) =>
        report.Assignments.Where(a => a.Role == role).Select(a => a.UserId);

    /// <summary>報告編號：{類別}-{yyyyMM}-{四碼流水號}；唯一索引保障並行時不重號</summary>
    private async Task<string> NextReportNoAsync(string categoryCode, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var prefix = $"{categoryCode}-{now.ToOffset(TimeSpan.FromHours(8)):yyyyMM}-";
        var existing = await dbContext.Reports.AsNoTracking()
            .Where(r => r.ReportNo.StartsWith(prefix) && r.Version == 1)
            .Select(r => r.ReportNo)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        int max = existing
            .Select(no => int.TryParse(no.AsSpan(prefix.Length, Math.Min(4, no.Length - prefix.Length)), out var n) ? n : 0)
            .DefaultIfEmpty(0).Max();
        return $"{prefix}{max + 1:D4}";
    }

    private async Task<ReportPdfData> BuildPdfDataAsync(Report report, bool preview, string? watermark, CancellationToken cancellationToken)
    {
        string? supersedesNo = null;
        if (report.SupersedesReportId is { } previousId)
        {
            supersedesNo = await dbContext.Reports.AsNoTracking().Where(r => r.Id == previousId).Select(r => r.ReportNo)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        }

        var baseUrl = configuration["ReportSettings:VerificationBaseUrl"];
        return new ReportPdfData(
            report.ReportNo, report.Version, report.Title,
            await CategoryNameAsync(report.CategoryCode, cancellationToken).ConfigureAwait(false) ?? report.CategoryCode,
            await LabNameAsync(report.TenantLabId, cancellationToken).ConfigureAwait(false) ?? string.Empty,
            report.CustomerName, report.SampleNo, report.SampleDescription,
            report.Results.OrderBy(r => r.Sequence).Select(r => new ReportPdfResultRow(r.ItemName, r.Value, r.Unit, r.LimitText, r.Method)).ToList(),
            report.SignerName, report.SignerCertificateNo, report.SignedAt,
            report.VerificationToken,
            string.IsNullOrWhiteSpace(baseUrl) || report.VerificationToken is null ? null : $"{baseUrl.TrimEnd('/')}/{report.VerificationToken}",
            supersedesNo, preview, watermark);
    }

    private async Task<string?> CategoryNameAsync(string code, CancellationToken cancellationToken)
    {
        var categories = await categoryService.ListCategoriesAsync(cancellationToken).ConfigureAwait(false);
        return categories.FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase))?.Name;
    }

    private async Task<string?> LabNameAsync(Guid tenantLabId, CancellationToken cancellationToken) =>
        (await orgIntegrationService.GetOrganizationByIdAsync(tenantLabId, cancellationToken).ConfigureAwait(false))?.Name;

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
