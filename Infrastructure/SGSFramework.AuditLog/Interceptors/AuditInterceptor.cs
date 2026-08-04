using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Entities.AuditLogs;
using SGSFramework.Core.HttpAuditProviders;
using SGSFramework.Core.Mask;
using SGSFramework.AuditLog.Channels;
using SGSFramework.AuditLog.Configurations;
using SGSFramework.AuditLog.DTOs;
using System.Reflection;

namespace SGSFramework.AuditLog.Interceptors
{
    public class AuditInterceptor : SaveChangesInterceptor
    {
        private const string DefaultSystemUser = "SYSTEM";
        private const string DefaultTraceId = "SYSTEM_BACKGROUND";

        private readonly IServiceProvider _serviceProvider;
        private readonly AuditChannel _channel;
        private readonly ILogger<AuditInterceptor> _logger;
        private readonly IOptionsMonitor<AuditOptions> _options;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IAuditProvider _fallbackAuditProvider;

        public AuditInterceptor(
            IServiceProvider serviceProvider,
            IOptionsMonitor<AuditOptions> options,
            AuditChannel channel,
            ILogger<AuditInterceptor> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
            _fallbackAuditProvider = new SystemAuditProvider(DefaultSystemUser, DefaultTraceId);
        }

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            ArgumentNullException.ThrowIfNull(eventData);

            if (eventData.Context is not null)
            {
                var settings = _options.CurrentValue;
                if (!settings.IsEnabled)
                    return base.SavingChanges(eventData, result);

                try
                {
                    var auditProvider = ResolveAuditProvider(eventData.Context);
                    OnBeforeSaveChangesSync(eventData.Context, auditProvider);

                    var userId = auditProvider.UserId ?? DefaultSystemUser;
                    UpdateAuditableEntities(eventData.Context, userId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "OnBeforeSaveChangesSync 執行階段發生錯誤。");
                }
            }

            return base.SavingChanges(eventData, result);
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(eventData);

            if (eventData.Context is not null)
            {
                var settings = _options.CurrentValue;
                if (!settings.IsEnabled)
                    return await base.SavingChangesAsync(eventData, result, cancellationToken);

                try
                {
                    var auditProvider = ResolveAuditProvider(eventData.Context);

                    await OnBeforeSaveChangesAsync(eventData.Context, auditProvider, cancellationToken).ConfigureAwait(false);

                    var userId = auditProvider.UserId ?? DefaultSystemUser;
                    UpdateAuditableEntities(eventData.Context, userId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "OnBeforeSaveChangesAsync 執行階段發生錯誤。");
                }
            }

            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        /// <summary>
        /// 安全解析 IAuditProvider，無 HttpContext 或解析失敗時回傳預設系統身分 (Null Object Pattern)
        /// </summary>
        private IAuditProvider ResolveAuditProvider(DbContext context)
        {
            try
            {
                var httpContext = _httpContextAccessor.HttpContext;
                if (httpContext?.RequestServices is { } requestServices)
                {
                    var provider = requestServices.GetService<IAuditProvider>();
                    if (provider != null)
                    {
                        return provider;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "無法從 HttpContext 解析 IAuditProvider，將自動降級採用系統預設身分。");
            }

            return _fallbackAuditProvider;
        }

        private static void UpdateAuditableEntities(DbContext context, string? userId)
        {
            var effectiveUserId = string.IsNullOrWhiteSpace(userId) ? DefaultSystemUser : userId;
            IEnumerable<EntityEntry<IAuditable>> entries = context.ChangeTracker.Entries<IAuditable>();

            foreach (EntityEntry<IAuditable> entry in entries)
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.Entity.CreatedBy ??= effectiveUserId;
                        entry.Entity.CreatedAtUtc = DateTime.UtcNow;
                        break;
                    case EntityState.Modified:
                        entry.Entity.UpdatedBy = effectiveUserId;
                        entry.Entity.UpdatedAtUtc = DateTime.UtcNow;
                        break;
                }
            }
        }

        private void OnBeforeSaveChangesSync(DbContext context, IAuditProvider auditProvider)
        {
            var settings = _options.CurrentValue;
            var auditEntries = CaptureAuditEntries(auditProvider, settings, context);
            if (auditEntries.Count == 0) return;

            foreach (var entry in auditEntries)
            {
                try
                {
                    if (!_channel.TryAddAuditLog(entry))
                    {
                        _logger.LogWarning("Channel 已滿，嘗試阻塞式寫入以避免遺失審計資料。");
                        _channel.AddAuditLogSync(entry);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "在同步路徑寫入 Channel 時發生錯誤，審計可能遺失。");
                }
            }
        }

        private async Task OnBeforeSaveChangesAsync(DbContext context, IAuditProvider auditProvider, CancellationToken ct)
        {
            var settings = _options.CurrentValue;
            var auditEntries = CaptureAuditEntries(auditProvider, settings, context);
            if (auditEntries.Count == 0) return;

            try
            {
                await _channel.AddBatchAuditLogAsync(auditEntries, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("審計寫入被取消 (CancellationToken)。");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "在非同步路徑寫入 Channel 時發生錯誤。");
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
                if (entry.Entity is AuditLogEntity ||
                    entry.State == EntityState.Detached ||
                    entry.State == EntityState.Unchanged) continue;

                if (options.IgnoredTables.Contains(entry.Metadata.Name)) continue;

                var entityType = entry.Metadata;
                var clrType = entry.Entity.GetType();

                if (entry.Entity is IAuditable && (entry.State == EntityState.Added || entry.State == EntityState.Modified || entry.State == EntityState.Deleted))
                {
                    try
                    {
                        var auditEntry = new AuditEntry(entry)
                        {
                            UserId = currentUserId,
                            TraceId = currentTraceId
                        };

                        bool isDeFactoJoin = entityType.FindPrimaryKey()?.Properties
                            .All(p => p.IsForeignKey()) == true;

                        bool isLinker = entityType.GetNavigations().Count() >= 2 &&
                                        entityType.GetProperties().Count() <= entityType.GetNavigations().Count() + 1;

                        if (isDeFactoJoin || isLinker)
                        {
                            HandleJoinTableAudit(entry);
                            auditEntry.EntryType = AuditEntryType.Relationship;

                            var properties = entry.Properties.ToList();
                            if (properties.Count >= 2)
                            {
                                auditEntry.RelationInfo = new RelationMetadata
                                {
                                    LeftEntityName = properties[0].Metadata.Name.Replace("Id", ""),
                                    LeftId = properties[0].CurrentValue?.ToString() ?? string.Empty,
                                    RightEntityName = properties[1].Metadata.Name.Replace("Id", ""),
                                    RightId = properties[1].CurrentValue?.ToString() ?? string.Empty
                                };

                                auditEntry.NewValues["Relation"] = $"{auditEntry.RelationInfo.LeftEntityName}({auditEntry.RelationInfo.LeftId}) <-> {auditEntry.RelationInfo.RightEntityName}({auditEntry.RelationInfo.RightId})";
                            }
                        }

                        int validPropertyChangesCount = 0;

                        foreach (var property in entry.Properties)
                        {
                            string propertyName = property.Metadata.Name;
                            var propInfo = clrType.GetProperty(propertyName);

                            if (propInfo != null && propInfo.IsDefined(typeof(AuditIgnoreAttribute), true))
                            {
                                continue;
                            }

                            if (property.Metadata.IsPrimaryKey())
                            {
                                auditEntry.KeyValues[propertyName] = property.CurrentValue;
                                if (property.IsTemporary)
                                {
                                    auditEntry.TemporaryProperties.Add(property);
                                }
                                continue;
                            }

                            var encryptAttr = propInfo?.GetCustomAttribute<SensitiveDataAttribute>();
                            bool isSensitive = encryptAttr != null;
                            string label = isSensitive ? " (Encrypted)" : string.Empty;

                            var currentValue = isSensitive ? ApplyMasking(property.CurrentValue?.ToString(), encryptAttr!.Format) : property.CurrentValue?.ToString();
                            var originalValue = isSensitive ? ApplyMasking(property.OriginalValue?.ToString(), encryptAttr!.Format) : property.OriginalValue?.ToString();

                            switch (entry.State)
                            {
                                case EntityState.Added:
                                    auditEntry.NewValues[propertyName] = currentValue;
                                    validPropertyChangesCount++;
                                    break;

                                case EntityState.Deleted:
                                    auditEntry.OldValues[propertyName] = originalValue;
                                    validPropertyChangesCount++;
                                    break;

                                case EntityState.Modified:
                                    if (property.IsModified)
                                    {
                                        if (property.OriginalValue?.Equals(property.CurrentValue) == false)
                                        {
                                            auditEntry.ChangedColumns.Add($"{propertyName}{label}");
                                        }

                                        auditEntry.OldValues[propertyName] = originalValue;
                                        auditEntry.NewValues[propertyName] = currentValue;
                                        validPropertyChangesCount++;
                                    }
                                    break;
                            }
                        }

                        if (entry.State == EntityState.Modified && auditEntry.ChangedColumns.Count == 0 && validPropertyChangesCount == 0)
                        {
                            continue;
                        }

                        auditEntries.Add(auditEntry);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "在捕捉實體 {EntityName} 的審計資料時發生錯誤，該實體的變更將不會被記錄。", entry.Metadata.Name);
                    }
                }
            }

            return auditEntries;
        }

        public static string ApplyMasking(string? value, MaskFormat strategy)
        {
            if (string.IsNullOrWhiteSpace(value)) return value ?? string.Empty;

            return strategy switch
            {
                MaskFormat.IdCard when value.Length >= 10 =>
                    $"{value.Substring(0, 4)}****{value.Substring(value.Length - 2)}",
                //MaskFormat.Name when value.Length >= 2 =>
                //    $"{value.Substring(0, 1)}****",
                MaskFormat.Email when value.Contains('@') =>
                    $"{value.Split('@')[0].Substring(0, 1)}***@{value.Split('@')[1]}",
                MaskFormat.Phone when value.Length >= 10 =>
                    $"{value.Substring(0, 4)}****{value.Substring(value.Length - 2)}",
                MaskFormat.Address when value.Length >= 6 =>
                    $"{value.Substring(0, 3)}****{value.Substring(value.Length - 3)}",
                MaskFormat.CreditCard when value.Length >= 2 =>
                    $"{value.Substring(0, 1)}****",
                MaskFormat.BankAccount when value.Length >= 2 =>
                    $"{value.Substring(0, 1)}****",
                MaskFormat.TaxId when value.Length >= 8 =>
                    $"{value.Substring(0, 2)}****{value.Substring(value.Length - 2)}",
                MaskFormat.InvoiceCarrier when value.Length >= 8 =>
                    $"{value.Substring(0, 2)}****{value.Substring(value.Length - 2)}",
                _ => "******"
            };
        }

        private void HandleJoinTableAudit(EntityEntry entry)
        {
            var foreignKeys = entry.Metadata.GetForeignKeys();

            foreach (var fk in foreignKeys)
            {
                var principalType = fk.PrincipalEntityType.Name;
                var properties = fk.Properties;

                foreach (var prop in properties)
                {
                    var val = entry.CurrentValues[prop.Name];
                }
            }
        }

        /// <summary>
        /// 背景任務預設身分提供者實作
        /// </summary>
        private sealed class SystemAuditProvider : IAuditProvider
        {
            public string UserId { get; }
            public string UserName { get; }
            public string TraceId { get; }

            public string? RemoteIp { get; }
            public string? DeviceId { get; }      // 修正：補齊系統升級所需之設備識別碼
            public string? LaboratoryId { get; }  // 修正：補齊系統升級所需之實驗室識別碼

            public SystemAuditProvider(string userId, string traceId)
            {
                UserId = userId;
                TraceId = traceId;
            }
        }
    }
}