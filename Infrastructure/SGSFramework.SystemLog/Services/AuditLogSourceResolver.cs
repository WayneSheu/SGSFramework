#nullable enable
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Entities.AuditLogs;
using SGSFramework.SystemLog.DTOs;
using System.Reflection;

namespace SGSFramework.SystemLog.Services
{
    /// <summary>
    /// 資料日誌來源解析：找出所有「已註冊且模型含 AuditLogEntity」的 DbContext（主系統與各外掛模組，如 ORG），
    /// 供資料日誌查詢依來源動態切換。
    /// </summary>
    public interface IAuditLogSourceResolver
    {
        /// <summary>列出可查詢的資料日誌來源（主系統固定排第一）</summary>
        IReadOnlyList<AuditLogSourceDto> GetSources();

        /// <summary>
        /// 取得指定來源的唯讀查詢串流；<paramref name="sourceKey"/> 為空時使用主系統。
        /// 找不到來源時回傳 null。
        /// </summary>
        IQueryable<AuditLogEntity>? GetQueryable(string? sourceKey);
    }

    /// <remarks>
    /// 動態模組 (如 ORG) 載入於獨立 AssemblyLoadContext，同名 DbContext 型別可能同時存在於 Host 直接參考的組件中。
    /// 因此 DbContext 以 DI 實際可解析者為準，不依型別名稱挑選；AuditLogEntity 由 Core 共用，以該 DbContext 的模型確認。
    /// </remarks>
    public sealed class AuditLogSourceResolver : IAuditLogSourceResolver
    {
        private const string MainSchema = "core";

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AuditLogSourceResolver> _logger;

        public AuditLogSourceResolver(IServiceProvider serviceProvider, ILogger<AuditLogSourceResolver> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IReadOnlyList<AuditLogSourceDto> GetSources() =>
            Discover().Select(s => s.Dto).ToList();

        public IQueryable<AuditLogEntity>? GetQueryable(string? sourceKey)
        {
            var sources = Discover();
            var index = string.IsNullOrWhiteSpace(sourceKey)
                ? (sources.Count > 0 ? 0 : -1)
                : sources.FindIndex(s => string.Equals(s.Dto.Key, sourceKey.Trim(), StringComparison.OrdinalIgnoreCase));

            return index < 0 ? null : sources[index].Context.Set<AuditLogEntity>().AsNoTracking();
        }

        private List<(AuditLogSourceDto Dto, DbContext Context)> Discover()
        {
            var found = new List<(AuditLogSourceDto Dto, DbContext Context)>();

            foreach (var contextType in FindDbContextTypes())
            {
                // 僅取 DI 實際註冊者，避免挑到另一個 ALC 的同名型別
                if (_serviceProvider.GetService(contextType) is not DbContext context) continue;

                var entityType = context.Model.FindEntityType(typeof(AuditLogEntity));
                if (entityType is null) continue;

                var schema = entityType.GetSchema() ?? context.Model.GetDefaultSchema() ?? MainSchema;
                var table = entityType.GetTableName() ?? "AuditLogs";
                var isMain = string.Equals(schema, MainSchema, StringComparison.OrdinalIgnoreCase);
                var name = isMain ? "主系統" : $"{schema.ToUpperInvariant()} 模組";

                found.Add((new AuditLogSourceDto(contextType.Name, $"{name}（{schema}.{table}）", schema, isMain), context));
            }

            return found
                .DistinctBy(s => s.Dto.Key)
                .OrderByDescending(s => s.Dto.IsMain)
                .ThenBy(s => s.Dto.Schema, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private IEnumerable<Type> FindDbContextTypes() =>
            AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic)
                .SelectMany(a =>
                {
                    try
                    {
                        return a.GetTypes();
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        _logger.LogDebug(ex, "[AuditLogSourceResolver] 略過無法完整載入的組件 {Assembly}", a.FullName);
                        return ex.Types.OfType<Type>();
                    }
                })
                .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
                            && t != typeof(DbContext)
                            && typeof(DbContext).IsAssignableFrom(t));
    }
}
