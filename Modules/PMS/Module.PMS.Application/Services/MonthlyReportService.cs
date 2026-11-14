using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGS.Modules.PMS.Application.Contracts;
using SGS.Modules.PMS.Application.Rules;
using SGS.Modules.PMS.Infrastructure.Dbcontexts;
using SGS.Modules.PMS.Infrastructure.Entities;
using SGSFramework.Core.FileManagement;
using SGSFramework.Core.Paginations;

namespace SGS.Modules.PMS.Application.Services;

/// <summary>
/// 月度儀表板與月報 PDF（SOP 7.2）：依問題單統計總進線單數、SLA 準時達成率、MTTR、類型佔比；
/// PM／Tech Lead 產生 PDF 存入 FMS（分類 pms-form，加密儲存，僅能經 PMS 以 Internal 存取），專案內部成員可下載。
/// 每專案每月一份，重新產生會取代舊檔（版次遞增）。專案資料列授權：非內部成員一律 404。
/// </summary>
public sealed class MonthlyReportService(
    PMSDbContext db,
    IFileManagementService files,
    HolidayService holidays,
    PmsMailNotifier notifier,
    TimeProvider clock,
    ILogger<MonthlyReportService> logger)
{
    public const string FileCategory = "pms-form";
    public const string ReferenceType = "PmsMonthlyReport";

    private const int MaxPageSize = 200;

    private const string AutoActor = "system:pms-report-monthly";

    // ---------- 儀表板 ----------

    public async Task<PmsResult<MonthlyDashboardDto>> GetDashboardAsync(Guid userId, Guid projectId, string? period, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        period = string.IsNullOrWhiteSpace(period) ? MonthlyStatsCalculator.FormatPeriod(now) : period.Trim();
        if (!MonthlyStatsCalculator.TryParsePeriod(period, out var start, out var end))
            return PmsError.Validation("period 格式須為 YYYY-MM（例：2026-11）。");

        var project = await LoadProjectAsync(userId, projectId, ct).ConfigureAwait(false);
        if (project.Error is { } error) return error;
        var p = project.Value!;

        var stats = await ComputeAsync(p.Project.Id, period, start, end, now, ct).ConfigureAwait(false);
        var canGenerate = ProjectAccessPolicy.IsPm(userId, p.Members) || ProjectAccessPolicy.IsTechLead(userId, p.Members);
        return PmsResult<MonthlyDashboardDto>.Success(new MonthlyDashboardDto(
            p.Project.Id, p.Project.Code, p.Project.Name, period, now >= start && now < end, stats.AsOf,
            stats.TotalIncoming, stats.SlaApplicableCount, stats.SlaMetOnTimeCount, stats.SlaOnTimeRate, stats.MttrHours,
            stats.TypeRatio, stats.OverdueCount, stats.DueSoonCount, canGenerate));
    }

    // ---------- 產生 PDF ----------

    public async Task<PmsResult<MonthlyReportDto>> GenerateAsync(Guid userId, string actor, GenerateMonthlyReportRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = clock.GetUtcNow();
        var period = request.Period?.Trim() ?? string.Empty;
        if (!MonthlyStatsCalculator.TryParsePeriod(period, out var start, out var end))
            return PmsError.Validation("period 格式須為 YYYY-MM（例：2026-11）。");
        if (start > now) return PmsError.Validation("不可產生未來月份的月報。");

        var project = await LoadProjectAsync(userId, request.ProjectId, ct).ConfigureAwait(false);
        if (project.Error is { } error) return error;
        var p = project.Value!;
        if (!ProjectAccessPolicy.IsPm(userId, p.Members) && !ProjectAccessPolicy.IsTechLead(userId, p.Members))
            return PmsError.Forbidden("僅專案 PM 或 Tech Lead 可產生月報。");

        return await GenerateCoreAsync(p.Project, period, start, end, userId, actor, now, replaceExisting: true, ct).ConfigureAwait(false);
    }

    /// <summary>產生月報核心：統計 → PDF → FMS → 中繼資料；<paramref name="replaceExisting"/> 為 false 時已有月報則不覆蓋。</summary>
    private async Task<PmsResult<MonthlyReportDto>> GenerateCoreAsync(
        Project project, string period, DateTimeOffset start, DateTimeOffset end, Guid generatedBy, string actor, DateTimeOffset now,
        bool replaceExisting, CancellationToken ct)
    {
        var stats = await ComputeAsync(project.Id, period, start, end, now, ct).ConfigureAwait(false);

        var existing = await db.MonthlyReports.FirstOrDefaultAsync(r => r.ProjectId == project.Id && r.Period == period, ct).ConfigureAwait(false);
        if (existing is not null && !replaceExisting)
            return PmsError.Conflict("PMS.REPORT_EXISTS", $"{project.Code} {period} 月報已存在，自動產生不覆蓋。");

        var revision = (existing?.Revision ?? 0) + 1;

        var pdf = MonthlyReportPdf.Render(project.Code, project.Name, stats, revision, now);
        var fileName = $"{now.ToOffset(SlaCalculator.Offset):yyyyMMdd}_{project.Code}_月度維運報告_{period}_v{revision}.pdf";
        var sha = Convert.ToHexString(SHA256.HashData(pdf)).ToLowerInvariant();
        var reportId = existing?.Id ?? Guid.NewGuid();

        FileMetadataDto stored;
        try
        {
            using var stream = new MemoryStream(pdf, writable: false);
            stored = await files.UploadAsync(
                new FileUploadRequest(stream, fileName, FileCategory, ReferenceType, reportId.ToString(), sha),
                FileAccessScope.Internal, ct).ConfigureAwait(false);
        }
        catch (FileRejectedException ex)
        {
            return MapRejected(ex);
        }

        var oldFileId = existing?.FileId;
        try
        {
            if (existing is null)
            {
                existing = new MonthlyReport { Id = reportId, ProjectId = project.Id, Period = period, CreatedBy = actor, CreatedAtUtc = now };
                db.MonthlyReports.Add(existing);
            }
            else
            {
                existing.UpdatedBy = actor;
                existing.UpdatedAtUtc = now;
            }

            existing.Revision = revision;
            existing.FileId = stored.Id;
            existing.FileName = fileName;
            existing.FileSize = stored.FileSize;
            existing.Sha256 = stored.Sha256;
            existing.TotalIncoming = stats.TotalIncoming;
            existing.SlaOnTimeRate = stats.SlaOnTimeRate;
            existing.MttrHours = stats.MttrHours;
            existing.OverdueCount = stats.OverdueCount;
            existing.DueSoonCount = stats.DueSoonCount;
            existing.GeneratedBy = generatedBy;
            existing.GeneratedAt = now;

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is DbUpdateException or DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            await CompensateAsync(stored.Id).ConfigureAwait(false);
            logger.LogWarning(ex, "[PMS] 月報中繼資料寫入失敗：{ProjectId} {Period}", project.Id, period);
            return PmsError.Conflict("PMS.CONCURRENCY_CONFLICT", "月報剛被他人產生或更新，請重新整理後再試。");
        }

        // 舊版 PDF 由 FMS 虛擬刪除（失敗不影響新版，僅記錄）
        if (oldFileId is { } old && old != stored.Id)
        {
            try
            {
                await files.DeleteAsync(old, FileAccessScope.Internal, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[PMS] 月報舊版 PDF {FileId} 回收失敗，請維運清理。", old);
            }
        }

        db.ChangeTracker.Clear();
        return PmsResult<MonthlyReportDto>.Success(ToDto(existing!, project));
    }

    // ---------- 排程自動產生 ----------

    /// <summary>
    /// 排程（SCH 作業 pms-report-monthly，每天執行）：月底最後工作日產生當月月報，月初前 3 個工作日補產上月缺漏者；
    /// <paramref name="period"/> 指定時（管理者手動回補）則處理該月，不看今天是否為產生日。
    /// 只處理進行中且在維運期（SLA）的專案；已有月報者不覆蓋（PM 手動產生的版本優先）；
    /// 產生後 Email 通知該專案 PM／Tech Lead 審閱並轉交客戶。
    /// </summary>
    public async Task<PmsResult<MonthlyAutoGenerateResult>> AutoGenerateAsync(string? period, CancellationToken ct)
    {
        var now = clock.GetUtcNow();

        IReadOnlyList<string> periods;
        if (!string.IsNullOrWhiteSpace(period))
        {
            var manual = period.Trim();
            if (!MonthlyStatsCalculator.TryParsePeriod(manual, out var manualStart, out _))
                return PmsError.Validation("period 格式須為 YYYY-MM（例：2026-11）。");
            if (manualStart > now) return PmsError.Validation("不可產生未來月份的月報。");
            periods = [manual];
        }
        else
        {
            var today = DateOnly.FromDateTime(now.ToOffset(SlaCalculator.Offset).DateTime);
            var (from, to) = MonthlyReportSchedule.MonthRange(today);
            var calendar = await holidays.LoadCalendarAsync(from, to, ct).ConfigureAwait(false);
            periods = MonthlyReportSchedule.TargetPeriods(now, calendar);
        }

        if (periods.Count == 0)
            return PmsResult<MonthlyAutoGenerateResult>.Success(new MonthlyAutoGenerateResult(
                [], 0, 0, 0, 0, 0, "今天不是月報產生日（月底最後工作日；月初前 3 個工作日補產上月），略過。"));

        var projects = await db.Projects.AsNoTracking().Include(p => p.Members)
            .Where(p => p.Status == ProjectStatus.Active && p.Stage == ProjectStage.Sla)
            .ToListAsync(ct).ConfigureAwait(false);

        int eligible = 0, generated = 0, skipped = 0, failed = 0, notified = 0;
        foreach (var month in periods)
        {
            MonthlyStatsCalculator.TryParsePeriod(month, out var start, out var end);

            foreach (var project in projects)
            {
                ct.ThrowIfCancellationRequested();
                eligible++;

                if (await db.MonthlyReports.AsNoTracking().AnyAsync(r => r.ProjectId == project.Id && r.Period == month, ct).ConfigureAwait(false))
                {
                    skipped++;
                    continue;
                }

                var result = await GenerateCoreAsync(project, month, start, end, Guid.Empty, AutoActor, now, replaceExisting: false, ct).ConfigureAwait(false);
                if (!result.IsSuccess)
                {
                    if (result.Error!.Code == "PMS.REPORT_EXISTS") { skipped++; continue; }

                    failed++;
                    logger.LogWarning("[PMS] 月報自動產生失敗：{ProjectCode} {Period}：{Detail}", project.Code, month, result.Error.Detail);
                    continue;
                }

                generated++;
                if (await NotifyAsync(project, result.Value!, now, ct).ConfigureAwait(false)) notified++;
            }

            db.ChangeTracker.Clear();
        }

        var summary = $"處理 {string.Join("、", periods)}：符合 {eligible} 個專案次，新產生 {generated}、已存在略過 {skipped}"
                      + (failed > 0 ? $"、失敗 {failed}" : null) + (generated > 0 ? $"，已通知 {notified} 個專案的 PM／Tech Lead" : null);
        logger.LogInformation("[PMS] 月報自動產生：{Summary}", summary);
        return PmsResult<MonthlyAutoGenerateResult>.Success(new MonthlyAutoGenerateResult(periods, eligible, generated, skipped, failed, notified, summary));
    }

    /// <summary>通知專案 PM／Tech Lead 月報已產生，請審閱後轉交客戶備查；找不到收件人或寄送失敗不影響月報。</summary>
    private async Task<bool> NotifyAsync(Project project, MonthlyReportDto report, DateTimeOffset now, CancellationToken ct)
    {
        if (!notifier.IsAvailable) return false;

        var recipientIds = project.Members.Where(m => m.RaciRole is RaciRole.Pm or RaciRole.TechLead).Select(m => m.UserId);
        var emails = await notifier.ResolveEmailsAsync(recipientIds, ct).ConfigureAwait(false);
        if (emails.Count == 0)
        {
            logger.LogWarning("[PMS] 專案 {ProjectCode} 的 {Period} 月報已產生，但找不到 PM／Tech Lead 的 Email，未通知。", project.Code, report.Period);
            return false;
        }

        var rate = report.SlaOnTimeRate is { } r ? $"{r * 100:0.#}%" : "—";
        var mttr = report.MttrHours is { } m ? $"{m:0.##} 小時" : "—";
        var subject = $"{project.Code} {report.Period} 月度維運報告已產生，請審閱";
        var body = $"專案 {System.Net.WebUtility.HtmlEncode(project.Code)} {System.Net.WebUtility.HtmlEncode(project.Name)} 的 {report.Period} 月度維運報告已自動產生（第 {report.Revision} 版）。<br/><br/>"
                   + $"總進線單數：{report.TotalIncoming} 張<br/>SLA 準時達成率：{rate}<br/>平均修復時間（MTTR）：{mttr}<br/>已逾期未結案：{report.OverdueCount} 張<br/><br/>"
                   + "請 PM 於專案管理系統「月度維運報告」下載 PDF，審閱後轉交客戶備查；如需調整統計或補充說明，可重新產生（會取代此版本）。";

        return await notifier.SendAsync(subject, body, emails, "PMS 月報已產生", $"pms.report.monthly:{project.Id:N}:{report.Period}", now, ct).ConfigureAwait(false);
    }

    // ---------- 查詢與下載 ----------

    public async Task<PagedResult<MonthlyReportDto>> SearchAsync(Guid userId, MonthlyReportSearchRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var pageIndex = Math.Max(1, request.PageIndex);
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);
        var filter = request.Filter ?? new MonthlyReportSearchFilter();

        var projectIds = await db.ProjectMembers.AsNoTracking()
            .Where(m => m.UserId == userId && m.RaciRole != RaciRole.Stakeholder)
            .Select(m => m.ProjectId).Distinct().ToListAsync(ct).ConfigureAwait(false);

        var query = db.MonthlyReports.AsNoTracking().Where(r => projectIds.Contains(r.ProjectId));
        if (filter.ProjectId is { } projectId) query = query.Where(r => r.ProjectId == projectId);
        if (!string.IsNullOrWhiteSpace(filter.PeriodFrom)) { var from = filter.PeriodFrom.Trim(); query = query.Where(r => string.Compare(r.Period, from) >= 0); }
        if (!string.IsNullOrWhiteSpace(filter.PeriodTo)) { var to = filter.PeriodTo.Trim(); query = query.Where(r => string.Compare(r.Period, to) <= 0); }

        var total = await query.CountAsync(ct).ConfigureAwait(false);
        var items = await query.OrderByDescending(r => r.Period).ThenBy(r => r.Project!.Code)
            .Skip((pageIndex - 1) * pageSize).Take(pageSize)
            .Select(r => new MonthlyReportDto(
                r.Id, r.ProjectId, r.Project!.Code, r.Project!.Name, r.Period, r.Revision, r.FileName, r.FileSize,
                r.TotalIncoming, r.SlaOnTimeRate, r.MttrHours, r.OverdueCount, r.DueSoonCount, r.GeneratedBy, r.GeneratedAt))
            .ToListAsync(ct).ConfigureAwait(false);

        return new PagedResult<MonthlyReportDto>(items, total, pageIndex, pageSize);
    }

    public async Task<PmsResult<FileDownload>> OpenDownloadAsync(Guid userId, Guid reportId, CancellationToken ct)
    {
        var report = await db.MonthlyReports.AsNoTracking().FirstOrDefaultAsync(r => r.Id == reportId, ct).ConfigureAwait(false);
        if (report is null) return PmsError.NotFound("月報不存在。");

        var project = await LoadProjectAsync(userId, report.ProjectId, ct).ConfigureAwait(false);
        if (project.Error is { } error) return PmsError.NotFound("月報不存在。");

        try
        {
            var download = await files.OpenDownloadAsync(report.FileId, FileAccessScope.Internal, ct).ConfigureAwait(false);
            if (download is null) return PmsError.NotFound("月報檔案不存在或已被清除。");
            if (download.Content is null)
            {
                logger.LogWarning("[PMS] 月報 {ReportId} 的 FMS 分類回傳導向網址，已拒絕。", reportId);
                return PmsError.Conflict("PMS.FILE_REJECTED", "此檔案不支援經 PMS 下載，請聯絡管理員檢查 FMS 分類設定。");
            }

            return PmsResult<FileDownload>.Success(download);
        }
        catch (FileRejectedException ex)
        {
            return MapRejected(ex);
        }
    }

    // ---------- 內部輔助 ----------

    private sealed record ProjectContext(Project Project, IReadOnlyList<(Guid UserId, RaciRole Role)> Members);

    private async Task<PmsResult<ProjectContext>> LoadProjectAsync(Guid userId, Guid projectId, CancellationToken ct)
    {
        var project = await db.Projects.AsNoTracking().Include(p => p.Members).FirstOrDefaultAsync(p => p.Id == projectId, ct).ConfigureAwait(false);
        if (project is null) return PmsError.NotFound("專案不存在。");

        var members = project.Members.Select(m => (m.UserId, m.RaciRole)).ToList();
        if (ProjectAccessPolicy.Evaluate(userId, members) != ProjectAccessLevel.Internal) return PmsError.NotFound("專案不存在。");
        return PmsResult<ProjectContext>.Success(new ProjectContext(project, members));
    }

    private async Task<MonthlyStats> ComputeAsync(Guid projectId, string period, DateTimeOffset start, DateTimeOffset end, DateTimeOffset now, CancellationToken ct)
    {
        // 統計需涵蓋當月受理，以及當月修復完成／月底仍未結的較早受理單：取「月底前受理」的全部單據的精簡欄位
        var facts = await db.Tickets.AsNoTracking()
            .Where(t => t.ProjectId == projectId && t.ReceivedAt < end && t.Status != TicketStatus.Cancelled)
            .Select(t => new TicketFact(t.Type, t.Status, t.ReceivedAt, t.DueAt, t.ResolvedAt))
            .ToListAsync(ct).ConfigureAwait(false);

        return MonthlyStatsCalculator.Compute(period, facts, start, end, now);
    }

    private async Task CompensateAsync(Guid fileId)
    {
        try
        {
            await files.DeleteAsync(fileId, FileAccessScope.Internal, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[PMS] 月報中繼資料寫入失敗，且回收 FMS 檔案 {FileId} 失敗，請維運清理孤兒檔案。", fileId);
        }
    }

    private static MonthlyReportDto ToDto(MonthlyReport r, Project p) => new(
        r.Id, r.ProjectId, p.Code, p.Name, r.Period, r.Revision, r.FileName, r.FileSize,
        r.TotalIncoming, r.SlaOnTimeRate, r.MttrHours, r.OverdueCount, r.DueSoonCount, r.GeneratedBy, r.GeneratedAt);

    private static PmsError MapRejected(FileRejectedException ex) => ex.StatusCode switch
    {
        503 => new PmsError("PMS.FILE_SERVICE_UNAVAILABLE", 503, "Service Unavailable", ex.Message),
        413 => new PmsError("PMS.FILE_REJECTED", 413, "Payload Too Large", ex.Message),
        415 => new PmsError("PMS.FILE_REJECTED", 415, "Unsupported Media Type", ex.Message),
        403 => new PmsError("PMS.FILE_REJECTED", 403, "Forbidden", ex.Message),
        _ => new PmsError("PMS.FILE_REJECTED", 400, "Invalid Request", ex.Message)
    };
}
