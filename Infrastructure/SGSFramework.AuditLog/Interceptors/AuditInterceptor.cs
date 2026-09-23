// Path: Infrastructure/SGSFramework.AuditLog/Interceptors/AuditInterceptor.cs
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
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Entities.AuditLogs;
using SGSFramework.Core.HttpAuditProviders;
using SGSFramework.Core.Mask;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

public class AuditInterceptor : SaveChangesInterceptor
{
    private const string DefaultSystemUser = "SYSTEM";
    private const string DefaultTraceId = "SYSTEM_BACKGROUND";

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AuditInterceptor> _logger;
    private readonly IOptionsMonitor<AuditOptions> _options;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuditProvider _fallbackAuditProvider;

    public AuditInterceptor(
        IServiceProvider serviceProvider,
        IOptionsMonitor<AuditOptions> options,
        ILogger<AuditInterceptor> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _fallbackAuditProvider = new SystemAuditProvider(DefaultSystemUser, DefaultTraceId);
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
                await OnBeforeSaveChangesAsync(eventData.Context, auditProvider, cancellationToken).ConfigureAwait(false);

                var userId = auditProvider.UserId ?? DefaultSystemUser;
                UpdateAuditableEntities(eventData.Context, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AuditInterceptor] OnBeforeSaveChangesAsync 處理稽核紀錄時發生例外。");
            }
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken).ConfigureAwait(false);
    }

    private async Task OnBeforeSaveChangesAsync(DbContext context, IAuditProvider auditProvider, CancellationToken ct)
    {
        var settings = _options.CurrentValue;
        var auditEntries = CaptureAuditEntries(auditProvider, settings, context);
        if (auditEntries.Count == 0) return;

        var contextType = context.GetType();
        var channelType = typeof(IAuditChannel<>).MakeGenericType(contextType);
        var channel = _serviceProvider.GetService(channelType);

        if (channel is null)
        {
            _logger.LogWarning("[AuditInterceptor] 未為 {DbContext} 註冊專屬 IAuditChannel，將忽略此批次 {Count} 筆稽核紀錄。", contextType.Name, auditEntries.Count);
            return;
        }

        try
        {
            var addBatchMethod = channelType.GetMethod(nameof(IAuditChannel<object>.AddBatchAuditLogAsync));
            if (addBatchMethod is not null)
            {
                var task = (ValueTask)addBatchMethod.Invoke(channel, new object[] { auditEntries, ct })!;
                await task.ConfigureAwait(false);
                _logger.LogInformation("[AuditInterceptor] 成功派送 {Count} 筆稽核紀錄至 {DbContext} 管道。", auditEntries.Count, contextType.Name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AuditInterceptor] 派送至 {DbContext} 管道時發生錯誤。", contextType.Name);
        }
    }

    private List<AuditEntry> CaptureAuditEntries(IAuditProvider provider, AuditOptions options, DbContext context)
    {
        context.ChangeTracker.DetectChanges();
        var entries = context.ChangeTracker.Entries().ToList();
        var auditEntries = new List<AuditEntry>(entries.Count);

        var currentUserId = provider?.UserId ?? DefaultSystemUser;
        var currentTraceId = provider?.TraceId ?? DefaultTraceId;

        foreach (var entry in entries)
        {
            if (entry.Entity is AuditLogEntity || entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                continue;

            var entityTypeName = entry.Entity.GetType().Name;
            var tableName = entry.Metadata.GetTableName() ?? string.Empty;

            if (options.IgnoredTables.Contains(entry.Metadata.Name) || options.IgnoredTables.Contains(tableName))
                continue;

            // 包含一般 IAuditable 實體與所有權限配置實體 (UserGlobalPermission, UserLabPermission 等)
            bool isTargetEntity = entry.Entity is IAuditable ||
                                 entityTypeName.Contains("Permission", StringComparison.OrdinalIgnoreCase) ||
                                 tableName.Contains("Permissions", StringComparison.OrdinalIgnoreCase);

            if (!isTargetEntity) continue;

            try
            {
                var auditEntry = new AuditEntry(entry)
                {
                    UserId = currentUserId,
                    TraceId = currentTraceId
                };

                foreach (var property in entry.Properties)
                {
                    string propertyName = property.Metadata.Name;
                    if (property.Metadata.IsPrimaryKey())
                    {
                        auditEntry.KeyValues[propertyName] = property.CurrentValue;
                        continue;
                    }

                    var currentValue = property.CurrentValue?.ToString();
                    var originalValue = property.OriginalValue?.ToString();

                    switch (entry.State)
                    {
                        case EntityState.Added:
                            auditEntry.NewValues[propertyName] = currentValue;
                            break;
                        case EntityState.Deleted:
                            auditEntry.OldValues[propertyName] = originalValue;
                            break;
                        case EntityState.Modified:
                            if (property.IsModified && !Equals(originalValue, currentValue))
                            {
                                auditEntry.ChangedColumns.Add(propertyName);
                                auditEntry.OldValues[propertyName] = originalValue;
                                auditEntry.NewValues[propertyName] = currentValue;
                            }
                            break;
                    }
                }

                if (entry.State == EntityState.Modified && auditEntry.ChangedColumns.Count == 0)
                    continue;

                auditEntries.Add(auditEntry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AuditInterceptor] 捕捉實體 {EntityName} 變更失敗。", entityTypeName);
            }
        }

        return auditEntries;
    }

    private static void UpdateAuditableEntities(DbContext context, string userId)
    {
        var entries = context.ChangeTracker.Entries<IAuditable>();
        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedBy ??= userId;
                entry.Entity.CreatedAtUtc = DateTime.UtcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedBy = userId;
                entry.Entity.UpdatedAtUtc = DateTime.UtcNow;
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

        return _fallbackAuditProvider;
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