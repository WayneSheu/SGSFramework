// Path: Infrastructure/SGSFramework.AuditLog/Interceptors/PermissionAuditInterceptor.cs
namespace SGSFramework.AuditLog.Interceptors;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGSFramework.AuditLog.Abstractions;
using SGSFramework.AuditLog.Configurations;
using SGSFramework.AuditLog.DTOs;
using SGSFramework.AuditLog.Helpers;
using SGSFramework.Core.HttpAuditProviders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public class PermissionAuditInterceptor : SaveChangesInterceptor
{
    private const string DefaultSystemUser = "SYSTEM";
    private const string DefaultTraceId = "SYSTEM_BACKGROUND";

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PermissionAuditInterceptor> _logger;
    private readonly IOptionsMonitor<AuditOptions> _options;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PermissionAuditInterceptor(
        IServiceProvider serviceProvider,
        IOptionsMonitor<AuditOptions> options,
        ILogger<PermissionAuditInterceptor> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _options = options ?? throw new ArgumentNullException(nameof(options));
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
                _logger.LogError(ex, "[PermissionAuditInterceptor] 處理專屬權限變更日誌失敗。");
            }
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken).ConfigureAwait(false);
    }

    private async Task ProcessPermissionAuditsAsync(DbContext context, IAuditProvider auditProvider, CancellationToken ct)
    {
        context.ChangeTracker.DetectChanges();

        var permissionEntries = context.ChangeTracker.Entries()
            .Where(e => (e.Entity.GetType().Name.Contains("UserGlobalPermission", StringComparison.OrdinalIgnoreCase) ||
                         e.Entity.GetType().Name.Contains("UserLabPermission", StringComparison.OrdinalIgnoreCase) ||
                         e.Metadata.GetTableName()?.Contains("Permissions", StringComparison.OrdinalIgnoreCase) == true)
                    && (e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted))
            .ToList();

        if (permissionEntries.Count == 0) return;

        var userId = auditProvider.UserId ?? DefaultSystemUser;
        var traceId = auditProvider.TraceId ?? DefaultTraceId;
        var auditEntries = new List<AuditEntry>(permissionEntries.Count);

        foreach (var entry in permissionEntries)
        {
            try
            {
                var auditEntry = new AuditEntry(entry)
                {
                    UserId = userId,
                    TraceId = traceId
                };

                var userIdProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name.Equals("UserId", StringComparison.OrdinalIgnoreCase));
                var permKeyProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name.Equals("PermissionKey", StringComparison.OrdinalIgnoreCase) ||
                                                                     p.Metadata.Name.Equals("LabId", StringComparison.OrdinalIgnoreCase));
                var bitmaskProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name.Equals("Bitmask", StringComparison.OrdinalIgnoreCase));

                auditEntry.KeyValues["UserId"] = userIdProp?.CurrentValue?.ToString() ?? string.Empty;
                if (permKeyProp != null)
                {
                    auditEntry.KeyValues[permKeyProp.Metadata.Name] = permKeyProp.CurrentValue?.ToString() ?? string.Empty;
                }

                long oldBitmask = entry.State == EntityState.Added || bitmaskProp == null ? 0 : Convert.ToInt64(bitmaskProp.OriginalValue);
                long newBitmask = entry.State == EntityState.Deleted || bitmaskProp == null ? 0 : Convert.ToInt64(bitmaskProp.CurrentValue);

                auditEntry.OldValues["Bitmask"] = oldBitmask;
                auditEntry.NewValues["Bitmask"] = newBitmask;
                auditEntry.NewValues["PermissionSummary"] = PermissionAuditHelper.FormatPermissionChanges(oldBitmask, newBitmask);

                auditEntry.ChangedColumns.Add("Bitmask");
                auditEntry.ChangedColumns.Add("PermissionSummary");

                auditEntries.Add(auditEntry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PermissionAuditInterceptor] 轉換權限實體 {Entity} 變更失敗。", entry.Metadata.Name);
            }
        }

        if (auditEntries.Count == 0) return;

        var contextType = context.GetType();
        var channelType = typeof(IAuditChannel<>).MakeGenericType(contextType);
        var channel = _serviceProvider.GetService(channelType);

        if (channel is not null)
        {
            var addBatchMethod = channelType.GetMethod(nameof(IAuditChannel<object>.AddBatchAuditLogAsync));
            if (addBatchMethod is not null)
            {
                var task = (ValueTask)addBatchMethod.Invoke(channel, new object[] { auditEntries, ct })!;
                await task.ConfigureAwait(false);
            }
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
        catch { }

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