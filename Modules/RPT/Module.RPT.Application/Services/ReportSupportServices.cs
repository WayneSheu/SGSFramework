using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using SGS.Modules.RPT.Infrastructure.Dbcontexts;
using SGS.Modules.RPT.Infrastructure.Entities;
using SGSFramework.AuthTokenBucket.Hubs;
using SGSFramework.ReportEngine.Abstractions;

namespace SGS.Modules.RPT.Application.Services;

/// <summary>
/// 盲樣遮蔽：盲樣報告對無「檢視盲樣識別」授權者隱藏客戶與樣品來源，檢驗人員僅能看到樣品編號（防範利益衝突）。
/// </summary>
public static class ReportFieldMasker
{
    public const string MaskedText = "【盲樣遮蔽】";

    public static string? Mask(string? value, bool canSee) =>
        canSee || string.IsNullOrEmpty(value) ? value : MaskedText;
}

/// <summary>
/// 報告存取稽核（檢視、下載、列印、真偽查驗）：寫入 Ledger 僅附加資料表 rpt.ReportAccessLogs。
/// 同時實作 ReportEngine 的 <see cref="IReportAccessLogger"/>，使框架報表下載亦留下紀錄。
/// 寫入失敗僅記錄錯誤，不中斷使用者操作。
/// </summary>
public sealed class ReportAccessLogger(
    RPTDbContext dbContext,
    IHttpContextAccessor httpContextAccessor,
    TimeProvider timeProvider,
    ILogger<ReportAccessLogger> logger) : IReportAccessLogger
{
    public Task LogAsync(ClaimsPrincipal user, string reportTypeCode, string? reportKey, ReportAccessAction action, CancellationToken cancellationToken = default) =>
        WriteAsync(user, null, null, reportTypeCode, action, reportKey is null ? null : $"key={reportKey}", cancellationToken);

    public Task LogReportAsync(Report report, ReportAccessAction action, string? detail = null, CancellationToken cancellationToken = default) =>
        WriteAsync(httpContextAccessor.HttpContext?.User, report.Id, report.ReportNo, report.CategoryCode, action, detail, cancellationToken);

    private async Task WriteAsync(
        ClaimsPrincipal? user, Guid? reportId, string? reportNo, string typeCode, ReportAccessAction action, string? detail, CancellationToken cancellationToken)
    {
        try
        {
            var http = httpContextAccessor.HttpContext;
            var userAgent = http?.Request.Headers.UserAgent.ToString();
            var isAuthenticated = user?.Identity?.IsAuthenticated == true;

            // 存取紀錄獨立 SaveChanges，避免夾帶業務異動；使用獨立追蹤實體後立即分離
            var entry = dbContext.ReportAccessLogs.Add(new ReportAccessLog
            {
                ReportId = reportId,
                ReportNo = reportNo,
                ReportTypeCode = typeCode.Length > 64 ? typeCode[..64] : typeCode,
                Action = action.ToString(),
                UserId = isAuthenticated ? user!.FindFirst(ClaimTypes.NameIdentifier)?.Value : null,
                UserName = isAuthenticated ? user!.Identity!.Name ?? "未知使用者" : "匿名",
                RemoteIp = http?.Connection.RemoteIpAddress?.ToString(),
                UserAgent = string.IsNullOrEmpty(userAgent) ? null : userAgent.Length > 512 ? userAgent[..512] : userAgent,
                Detail = detail is { Length: > 500 } ? detail[..500] : detail,
                OccurredAt = timeProvider.GetUtcNow()
            });
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            entry.State = Microsoft.EntityFrameworkCore.EntityState.Detached;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "[ReportAccessLog] 寫入報告存取紀錄失敗：{ReportNo} {Action}", reportNo ?? typeCode, action);
        }
    }
}

/// <summary>
/// 報告流程通知：透過既有 SessionHub 的使用者群組即時推播「待審核 / 待簽署 / 退回 / 已發行」。
/// </summary>
public interface IReportWorkflowNotifier
{
    Task NotifyAsync(IEnumerable<Guid> userIds, Report report, string message, CancellationToken cancellationToken = default);
}

public sealed class ReportWorkflowNotifier(
    IHubContext<SessionHub> hubContext,
    ILogger<ReportWorkflowNotifier> logger) : IReportWorkflowNotifier
{
    /// <summary>前端訂閱的事件名稱</summary>
    public const string EventName = "ReportWorkflow";

    public async Task NotifyAsync(IEnumerable<Guid> userIds, Report report, string message, CancellationToken cancellationToken = default)
    {
        var targets = userIds.Distinct().Select(id => SessionHubGroups.User(id.ToString())).ToList();
        if (targets.Count == 0) return;

        try
        {
            await hubContext.Clients.Groups(targets).SendAsync(EventName, new
            {
                reportId = report.Id,
                reportNo = report.ReportNo,
                status = report.Status.ToString(),
                message
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "[ReportWorkflow] 推播通知失敗：{ReportNo}", report.ReportNo);
        }
    }
}
