#nullable enable
using SGSFramework.Core.Abstractions.Entities.AuditLogs;
using SGSFramework.Core.Logging;
using SGSFramework.SystemLog.DTOs;

namespace SGSFramework.SystemLog.Services
{
    /// <summary>
    /// 資料日誌來源解析：資料日誌一律存放於獨立日誌庫（SGS.Modules.LOG），來源為熱庫（現行）與年度換庫後的唯讀冷庫，
    /// 供資料日誌查詢依來源動態切換 / 複選。
    /// </summary>
    public interface IAuditLogSourceResolver
    {
        /// <summary>列出可查詢的資料日誌來源（熱庫固定排第一）</summary>
        IReadOnlyList<AuditLogSourceDto> GetSources();

        /// <summary>
        /// 取得指定來源的唯讀查詢串流；<paramref name="sourceKey"/> 為空時使用熱庫。
        /// 找不到來源（或日誌模組未啟用）時回傳 null。
        /// </summary>
        IQueryable<AuditLogEntity>? GetQueryable(string? sourceKey);
    }

    /// <remarks>
    /// 來源由 <see cref="ILogReadSource"/>（日誌外掛提供，含冷庫目錄）決定，不再掃描各模組 DbContext：
    /// 主庫與各模組已沒有 AuditLogs 資料表。
    /// </remarks>
    public sealed class AuditLogSourceResolver : IAuditLogSourceResolver
    {
        private const string LogSchema = "log";

        private readonly ILogReadSource? _readSource;

        public AuditLogSourceResolver(ILogReadSource? readSource = null) => _readSource = readSource;

        public IReadOnlyList<AuditLogSourceDto> GetSources() =>
            _readSource is null
                ? []
                : _readSource.Sources
                    .Select(s => new AuditLogSourceDto(s.Key, $"{s.DisplayName}（{LogSchema}.AuditLogs）", LogSchema, s.IsHot))
                    .ToList();

        public IQueryable<AuditLogEntity>? GetQueryable(string? sourceKey)
        {
            if (_readSource is null) return null;

            try
            {
                return _readSource.AuditLogsOf(sourceKey);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }
}
