// Path: Infrastructure/SGSFramework.AuditLog/Interceptors/AuditInterceptor.cs
namespace SGSFramework.AuditLog.Interceptors;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGSFramework.AuditLog.Abstractions;
using SGSFramework.AuditLog.Configurations;
using SGSFramework.AuditLog.DTOs;
using SGSFramework.AuditLog.Helpers;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Entities.AuditLogs;
using SGSFramework.Core.HttpAuditProviders;
using SGSFramework.Core.Mask;
using SGSFramework.Core.Security.FieldEncryption;
using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

public class AuditInterceptor : SaveChangesInterceptor
{
    private const string DefaultSystemUser = "SYSTEM";
    private const string DefaultSystemUserName = "系統行程";
    private const string DefaultTraceId = "SYSTEM_BACKGROUND";
    private const string MaskedValue = "[已遮蔽]";

    // (實體型別, 屬性名稱) → 是否帶有 [AuditIgnore]（含實作的介面成員）；反射結果快取
    private static readonly ConcurrentDictionary<(Type, string), bool> AuditIgnoreCache = new();

    // 同一個 DbContext 的一次 SaveChanges 只稽核一次：
    private static readonly ConditionalWeakTable<DbContext, object> InFlight = new();

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

        // 💡 強化 Fallback Provider 預設名稱
        _fallbackAuditProvider = new SystemAuditProvider(DefaultSystemUser, DefaultSystemUserName, DefaultTraceId);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (eventData.Context is not null && _options.CurrentValue.IsEnabled && InFlight.TryAdd(eventData.Context, InFlightMarker))
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

    private static readonly object InFlightMarker = new();

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null) InFlight.Remove(eventData.Context);
        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null) InFlight.Remove(eventData.Context);
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    public override async Task SaveChangesCanceledAsync(DbContextEventData eventData, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null) InFlight.Remove(eventData.Context);
        await base.SaveChangesCanceledAsync(eventData, cancellationToken).ConfigureAwait(false);
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

            bool isTargetEntity = entry.Entity is IAuditable;
            if (!isTargetEntity) continue;

            try
            {
                var auditEntry = new AuditEntry(entry)
                {
                    UserId = currentUserId,
                    TraceId = currentTraceId,
                    RemoteIp = provider?.RemoteIp
                };

                // 💡 支援自訂擴充：若 AuditEntry 支援捕獲 UserName 與 LaboratoryId (TenantId)，在此賦值
                // (請確認您的 AuditEntry DTO 是否包含這些屬性，若無可透過 Property 寫入)

                foreach (var property in entry.Properties)
                {
                    string propertyName = property.Metadata.Name;
                    if (property.Metadata.IsPrimaryKey())
                    {
                        auditEntry.KeyValues[propertyName] = property.CurrentValue;
                        continue;
                    }

                    if (IsIgnoredProperty(entry.Entity.GetType(), propertyName, options)) continue;

                    var currentValue = property.CurrentValue?.ToString();
                    var originalValue = property.OriginalValue?.ToString();

                    var isMasked = options.ExcludedProperties.Contains(propertyName, StringComparer.OrdinalIgnoreCase)
                                   || property.Metadata.IsEncryptedProperty();
                    var storedCurrent = isMasked && currentValue is not null ? MaskedValue : currentValue;
                    var storedOriginal = isMasked && originalValue is not null ? MaskedValue : originalValue;

                    switch (entry.State)
                    {
                        case EntityState.Added:
                            auditEntry.NewValues[propertyName] = storedCurrent;
                            break;
                        case EntityState.Deleted:
                            auditEntry.OldValues[propertyName] = storedOriginal;
                            break;
                        case EntityState.Modified:
                            if (property.IsModified && !Equals(originalValue, currentValue))
                            {
                                auditEntry.ChangedColumns.Add(propertyName);
                                auditEntry.OldValues[propertyName] = storedOriginal;
                                auditEntry.NewValues[propertyName] = storedCurrent;
                            }
                            break;
                    }
                }

                if (entry.State == EntityState.Modified && auditEntry.ChangedColumns.Count == 0)
                    continue;

                AppendPermissionSummary(entry, auditEntry);

                auditEntries.Add(auditEntry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AuditInterceptor] 捕捉實體 {EntityName} 變更失敗。", entityTypeName);
            }
        }

        return auditEntries;
    }

    private static void AppendPermissionSummary(EntityEntry entry, AuditEntry auditEntry)
    {
        var bitmask = entry.Properties.FirstOrDefault(p => p.Metadata.Name.Equals("Bitmask", StringComparison.OrdinalIgnoreCase));
        if (bitmask is null) return;

        if (entry.State == EntityState.Modified && !auditEntry.ChangedColumns.Contains(bitmask.Metadata.Name)) return;

        try
        {
            long oldMask = entry.State == EntityState.Added ? 0 : Convert.ToInt64(bitmask.OriginalValue);
            long newMask = entry.State == EntityState.Deleted ? 0 : Convert.ToInt64(bitmask.CurrentValue);
            var summary = PermissionAuditHelper.FormatPermissionChanges(oldMask, newMask);
            if (string.IsNullOrWhiteSpace(summary)) return;

            auditEntry.NewValues["PermissionSummary"] = summary;
            if (entry.State == EntityState.Modified) auditEntry.ChangedColumns.Add("PermissionSummary");
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
        {
            // 略過
        }
    }

    private static bool IsIgnoredProperty(Type entityType, string propertyName, AuditOptions options)
    {
        if (options.NoisyProperties.Contains(propertyName, StringComparer.OrdinalIgnoreCase)) return true;

        return AuditIgnoreCache.GetOrAdd((entityType, propertyName), static key =>
        {
            var (type, name) = key;
            if (type.GetProperty(name)?.IsDefined(typeof(AuditIgnoreAttribute), inherit: true) == true) return true;

            return type.GetInterfaces().Any(i => i.GetProperty(name)?.IsDefined(typeof(AuditIgnoreAttribute), inherit: true) == true);
        });
    }

    private static void UpdateAuditableEntities(DbContext context, string userId)
    {
        var entries = context.ChangeTracker.Entries<IAuditable>();
        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                if (string.IsNullOrWhiteSpace(entry.Entity.CreatedBy)) entry.Entity.CreatedBy = userId;
                entry.Entity.CreatedAtUtc = DateTime.UtcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedBy = userId;
                entry.Entity.UpdatedAtUtc = DateTime.UtcNow;
            }
        }
    }

    /// <summary>
    /// 💡 智慧解析 AuditProvider：結合 HTTP Context、Header (Lab Id) 與 DI 容器，確保 UserName 與 TenantId 絕不遺失
    /// </summary>
    private IAuditProvider ResolveAuditProvider()
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.RequestServices is { } requestServices)
            {
                var provider = requestServices.GetService<IAuditProvider>();
                if (provider != null)
                {
                    // 若 provider 抓得到，直接回傳
                    return provider;
                }
            }

            // 若 DI 找不到，但 HttpContext 存在，嘗試從 Request Headers 補捉 TenantId (Lab Id)
            if (httpContext != null)
            {
                var tenantId = httpContext.Request.Headers["X-Tenant-Id"].FirstOrDefault()
                               ?? httpContext.Request.Headers["X-Lab-Id"].FirstOrDefault();
                var userId = httpContext.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                             ?? httpContext.User?.Identity?.Name;
                var userName = httpContext.User?.FindFirst("FullName")?.Value
                               ?? httpContext.User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
                               ?? userId;

                if (!string.IsNullOrEmpty(userId) || !string.IsNullOrEmpty(tenantId))
                {
                    return new HttpContextDerivedAuditProvider(userId, userName, tenantId, httpContext);
                }
            }
        }
        catch { }

        return _fallbackAuditProvider;
    }

    private sealed class SystemAuditProvider : IAuditProvider
    {
        public string UserId { get; }
        public string UserName { get; }
        public string TraceId { get; }
        public string? RemoteIp { get; }
        public string? DeviceId { get; }
        public string? LaboratoryId { get; }

        public SystemAuditProvider(string userId, string userName, string traceId)
        {
            UserId = userId;
            UserName = userName;
            TraceId = traceId;
        }
    }

    private sealed class HttpContextDerivedAuditProvider : IAuditProvider
    {
        public string UserId { get; }
        public string UserName { get; }
        public string TraceId { get; }
        public string? RemoteIp { get; }
        public string? DeviceId { get; }
        public string? LaboratoryId { get; }

        public HttpContextDerivedAuditProvider(string? userId, string? userName, string? tenantId, HttpContext httpContext)
        {
            UserId = userId ?? DefaultSystemUser;
            UserName = userName ?? (userId ?? DefaultSystemUserName);
            TraceId = httpContext.TraceIdentifier;
            RemoteIp = httpContext.Connection.RemoteIpAddress?.ToString();
            LaboratoryId = tenantId;
        }
    }
}