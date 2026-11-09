using System.Security.Claims;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SGS.Modules.FMS.Infrastructure.Entities;
using SGSFramework.Core.Abstractions.Identities;

namespace SGS.Modules.FMS.Application.Auditing;

/// <summary>
/// 檔案存取稽核：於請求執行緒當下擷取身分（人員 / M2M 系統帳號）、來源 IP 與追蹤碼，
/// 放入有界佇列後由 <see cref="FileAccessLogWriter"/> 非同步批次寫入 fms.FileAccessLogs（不阻塞檔案傳輸）。
/// 佇列滿時等待而非丟棄，確保稽核軌跡完整。
/// </summary>
public interface IFileAccessAuditor
{
    ValueTask RecordAsync(string action, bool isSuccess, Guid? fileId = null, string? category = null, string? detail = null, CancellationToken cancellationToken = default);
}

/// <summary>稽核紀錄佇列（Singleton）</summary>
public sealed class FileAccessLogQueue
{
    public const int Capacity = 10_000;

    private readonly Channel<FileAccessLog> _channel = Channel.CreateBounded<FileAccessLog>(new BoundedChannelOptions(Capacity)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true
    });

    public ChannelWriter<FileAccessLog> Writer => _channel.Writer;

    public ChannelReader<FileAccessLog> Reader => _channel.Reader;
}

public sealed class FileAccessAuditor(
    FileAccessLogQueue queue,
    IHttpContextAccessor httpContextAccessor,
    TimeProvider timeProvider,
    ILogger<FileAccessAuditor> logger) : IFileAccessAuditor
{
    private const string CorrelationHeader = "X-Correlation-ID";

    public async ValueTask RecordAsync(string action, bool isSuccess, Guid? fileId = null, string? category = null, string? detail = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);

        var http = httpContextAccessor.HttpContext;
        var user = http?.User;
        var isAuthenticated = user?.Identity?.IsAuthenticated == true;
        var isSystemAccount = user.IsSystemAccount();
        var clientId = isSystemAccount ? user!.FindFirst(SystemAccountClaims.ClientId)?.Value : null;

        var entry = new FileAccessLog
        {
            FileId = fileId,
            Category = Truncate(category, 100),
            Action = action,
            IsSuccess = isSuccess,
            UserId = isAuthenticated ? Truncate(user!.FindFirst(ClaimTypes.NameIdentifier)?.Value, 64) : null,
            UserName = Truncate(ResolveUserName(http, user, isAuthenticated, isSystemAccount, clientId), 256)!,
            IsSystemAccount = isSystemAccount,
            ClientId = Truncate(clientId, 128),
            RemoteIp = Truncate(http?.Connection.RemoteIpAddress?.ToString(), 64),
            ForwardedFor = Truncate(http?.Request.Headers["X-Forwarded-For"].ToString(), 256),
            UserAgent = Truncate(http?.Request.Headers.UserAgent.ToString(), 512),
            TraceId = Truncate(http is null ? null : (string?)http.Request.Headers[CorrelationHeader].FirstOrDefault() ?? http.TraceIdentifier, 64),
            Detail = Truncate(detail, 500),
            OccurredAt = timeProvider.GetUtcNow()
        };

        try
        {
            // 用戶端中斷連線不應讓稽核紀錄遺失：已取消的權杖改以不可取消方式寫入
            await queue.Writer.WriteAsync(entry, cancellationToken.IsCancellationRequested ? CancellationToken.None : cancellationToken).ConfigureAwait(false);
        }
        catch (ChannelClosedException)
        {
            // 系統關機中：佇列已關閉，改寫入系統日誌保留證據
            logger.LogCritical("[FileAccessLog] 關機中無法排入稽核紀錄：{Action} {IsSuccess} 檔案 {FileId} 使用者 {UserId}/{UserName} IP {RemoteIp} {Detail}",
                entry.Action, entry.IsSuccess, entry.FileId, entry.UserId, entry.UserName, entry.RemoteIp, entry.Detail);
        }
    }

    private static string ResolveUserName(HttpContext? http, ClaimsPrincipal? user, bool isAuthenticated, bool isSystemAccount, string? clientId)
    {
        if (http is null) return "System";
        if (!isAuthenticated) return "匿名";
        if (isSystemAccount)
            return user!.FindFirst(SystemAccountClaims.DisplayName)?.Value ?? clientId ?? user.Identity!.Name ?? "系統帳號";
        return user!.Identity!.Name ?? "未知使用者";
    }

    private static string? Truncate(string? value, int maxLength) =>
        string.IsNullOrEmpty(value) ? null : value.Length <= maxLength ? value : value[..maxLength];
}
