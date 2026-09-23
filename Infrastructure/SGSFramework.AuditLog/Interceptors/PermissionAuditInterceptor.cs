// ==========================================
// 檔案路徑: Infrastructure/SGSFramework.AuditLog/Interceptors/PermissionAuditInterceptor.cs
// 架構層級: EF Core Interceptor
// ==========================================

namespace SGSFramework.AuditLog.Interceptors;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGSFramework.AuditLog.Channels;
using SGSFramework.AuditLog.Configurations;
using SGSFramework.AuditLog.DTOs;
using SGSFramework.AuditLog.Helpers;
using SGSFramework.Core.HttpAuditProviders;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public class PermissionAuditInterceptor : SaveChangesInterceptor
{
    private const string DefaultSystemUser = "SYSTEM";
    private const string DefaultTraceId = "SYSTEM_BACKGROUND";

    private readonly AuditChannel _channel;
    private readonly ILogger<PermissionAuditInterceptor> _logger;
    private readonly IOptionsMonitor<AuditOptions> _options;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PermissionAuditInterceptor(
        IOptionsMonitor<AuditOptions> options,
        AuditChannel channel,
        ILogger<PermissionAuditInterceptor> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (eventData.Context is not null && _options.CurrentValue.IsEnabled)
        {
            try
            {
                var auditProvider = ResolveAuditProvider();
                await ProcessPermissionAuditsAsync(eventData.Context, auditProvider, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PermissionAuditInterceptor] 處理權限稽核時發生例外。");
            }
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private async Task ProcessPermissionAuditsAsync(DbContext context, IAuditProvider auditProvider, CancellationToken ct)
    {
        context.ChangeTracker.DetectChanges();

        // 篩選 User_Global_Permissions 資料表對應的實體
        var permissionEntries = context.ChangeTracker.Entries()
            .Where(e => e.Metadata.Name.EndsWith("UserGlobalPermission", StringComparison.OrdinalIgnoreCase) ||
                        e.Metadata.GetTableName()?.Equals("User_Global_Permissions", StringComparison.OrdinalIgnoreCase) == true)
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted)
            .ToList();

        if (permissionEntries.Count == 0) return;

        var userId = auditProvider.UserId ?? DefaultSystemUser;
        var traceId = auditProvider.TraceId ?? DefaultTraceId;

        foreach (var entry in permissionEntries)
        {
            var auditEntry = new AuditEntry(entry)
            {
                UserId = userId,
                TraceId = traceId
            };

            var userIdVal = entry.Property("UserId").CurrentValue?.ToString() ?? string.Empty;
            var permKeyVal = entry.Property("PermissionKey").CurrentValue?.ToString() ?? string.Empty;

            long oldBitmask = entry.State == EntityState.Added ? 0 : Convert.ToInt64(entry.Property("Bitmask").OriginalValue);
            long newBitmask = entry.State == EntityState.Deleted ? 0 : Convert.ToInt64(entry.Property("Bitmask").CurrentValue);

            // 計算權限變更摘要
            var changeSummary = PermissionAuditHelper.FormatPermissionChanges(oldBitmask, newBitmask);

            auditEntry.KeyValues["UserId"] = userIdVal;
            auditEntry.KeyValues["PermissionKey"] = permKeyVal;

            auditEntry.OldValues["Bitmask"] = oldBitmask;
            auditEntry.NewValues["Bitmask"] = newBitmask;
            auditEntry.NewValues["PermissionSummary"] = changeSummary;

            auditEntry.ChangedColumns.Add("Bitmask");
            auditEntry.ChangedColumns.Add("PermissionSummary");

            // 派送至背景通道 AuditChannel
            await _channel.AddBatchAuditLogAsync(new[] { auditEntry }, ct).ConfigureAwait(false);
        }
    }

    private IAuditProvider ResolveAuditProvider()
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.RequestServices is { } requestServices)
            {
                var provider = requestServices.GetService<IAuditProvider>();
                if (provider != null) return provider;
            }
        }
        catch
        {
            // 降級採用預設身分
        }

        return new SystemAuditProvider(DefaultSystemUser, DefaultTraceId);
    }

    private sealed class SystemAuditProvider : IAuditProvider
    {
        public string UserId { get; }
        public string UserName { get; } = DefaultSystemUser;
        public string TraceId { get; }
        public string? RemoteIp { get; }
        public string? DeviceId { get; }
        public string? LaboratoryId { get; }

        public SystemAuditProvider(string userId, string traceId)
        {
            UserId = userId;
            TraceId = traceId;
        }
    }
}