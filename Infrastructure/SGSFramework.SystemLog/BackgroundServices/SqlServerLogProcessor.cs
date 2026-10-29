#nullable enable
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog.Events;
using SGSFramework.Core.Configurations;


namespace SGSFramework.SystemLog.BackgroundServices
{
    public interface ISqlServerLogProcessor
    {
        Task ProcessBatchAsync(IEnumerable<LogEvent> items, CancellationToken ct);
        Task FallbackAsync(IEnumerable<LogEvent> items, Exception ex);
    }

    public class SqlServerLogProcessor : ISqlServerLogProcessor
    {
        private readonly string _connectionString;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SqlServerLogProcessor> _logger;

        public SqlServerLogProcessor(
            IOptions<PersistentConnectionOptions> connectionOptions,
            IServiceScopeFactory scopeFactory,
            ILogger<SqlServerLogProcessor> logger)
        {
            ArgumentNullException.ThrowIfNull(connectionOptions);
            _connectionString = connectionOptions.Value.DefaultConnection;
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task ProcessBatchAsync(IEnumerable<LogEvent> items, CancellationToken ct)
        {
            var itemList = items.ToList();
            if (itemList.Count == 0) return;

            // 1. 批次解析真實的使用者中文姓名與實驗室名稱
            var (userMap, tenantMap) = await ResolveNamesBatchAsync(itemList, ct).ConfigureAwait(false);

            // 2. 建立帶有名稱對應的 DataTable
            var table = BuildDataTable(itemList, userMap, tenantMap);
            if (table.Rows.Count == 0) return;

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            using var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.CheckConstraints | SqlBulkCopyOptions.FireTriggers, null)
            {
                DestinationTableName = "core.SystemLogs",
                BatchSize = table.Rows.Count,
                BulkCopyTimeout = 30
            };

            ConfigureColumnMappings(bulkCopy);

            await bulkCopy.WriteToServerAsync(table, ct);
        }

        public Task FallbackAsync(IEnumerable<LogEvent> items, Exception ex)
        {
            _logger.LogWarning(ex, "[Fallback] 日誌持久化降級，改為本地降級處理機制。");
            return Task.CompletedTask;
        }

        /// <summary>
        /// 透過 Scope 批次查詢使用者真實姓名與實驗室名稱，避免 N+1 效能問題
        /// </summary>
        private async Task<(Dictionary<string, string> Users, Dictionary<string, string> Tenants)> ResolveNamesBatchAsync(
    List<LogEvent> items, CancellationToken ct)
        {
            var userMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var tenantMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var userIds = items
                    .Select(i => i.Properties.TryGetValue("UserId", out var u) ? u.ToString()?.Trim('"') : null)
                    .Where(id => !string.IsNullOrEmpty(id) && id != "System" && id != "SYSTEM" && id != "SYSTEM_BACKGROUND")
                    .Cast<string>()
                    .Distinct()
                    .ToList();

                var tenantIds = items
                    .Select(i => i.Properties.TryGetValue("TenantId", out var t) ? t.ToString()?.Trim('"') : null)
                    .Where(id => !string.IsNullOrEmpty(id))
                    .Cast<string>()
                    .Distinct()
                    .ToList();

                if (userIds.Count == 0 && tenantIds.Count == 0)
                {
                    return (userMap, tenantMap);
                }

                using var scope = _scopeFactory.CreateScope();

                // 💡 透過通用 DbContext 或介面解析，不再強綁定單一實體 Context
                var dbContext = scope.ServiceProvider.GetService<DbContext>();
                if (dbContext == null) return (userMap, tenantMap);

                // 1. 批次查詢使用者名稱
                if (userIds.Count > 0)
                {
                    var users = await dbContext.Database
                        .SqlQueryRaw<UserLookupDto>(
                            $"SELECT Id, COALESCE(FullName, UserName, Id) AS Name FROM core.Users WHERE Id IN ({string.Join(",", userIds.Select(id => $"'{id}'"))})")
                        .ToListAsync(ct)
                        .ConfigureAwait(false);

                    foreach (var u in users)
                    {
                        if (!string.IsNullOrEmpty(u.Id) && !string.IsNullOrEmpty(u.Name))
                        {
                            userMap[u.Id] = u.Name;
                        }
                    }
                }

                // 2. 批次查詢實驗室名稱
                if (tenantIds.Count > 0)
                {
                    var tenants = await dbContext.Database
                        .SqlQueryRaw<TenantLookupDto>(
                            $"SELECT Id, COALESCE(Name, Id) AS Name FROM org.Organization WHERE Id IN ({string.Join(",", tenantIds.Select(id => $"'{id}'"))})")
                        .ToListAsync(ct)
                        .ConfigureAwait(false);

                    foreach (var t in tenants)
                    {
                        if (!string.IsNullOrEmpty(t.Id) && !string.IsNullOrEmpty(t.Name))
                        {
                            tenantMap[t.Id] = t.Name;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[SqlServerLogProcessor] 批次解析使用者或實驗室名稱時發生例外，將採用降級文字。");
            }

            return (userMap, tenantMap);
        }



        private static DataTable BuildDataTable(
            IEnumerable<LogEvent> items,
            Dictionary<string, string> userMap,
            Dictionary<string, string> tenantMap)
        {
            var dt = new DataTable();
            dt.Columns.Add("TimeStamp", typeof(DateTimeOffset));
            dt.Columns.Add("CreatedAt", typeof(DateTimeOffset));
            dt.Columns.Add("Message", typeof(string));
            dt.Columns.Add("Level", typeof(string));
            dt.Columns.Add("Exception", typeof(string));
            dt.Columns.Add("TenantId", typeof(string));
            dt.Columns.Add("TenantName", typeof(string)); // 💡 新增 TenantName 欄位
            dt.Columns.Add("UserId", typeof(string));
            dt.Columns.Add("UserName", typeof(string));
            dt.Columns.Add("ModuleName", typeof(string));
            dt.Columns.Add("Operation", typeof(string));
            dt.Columns.Add("OperationDesc", typeof(string));
            dt.Columns.Add("CorrelationId", typeof(string));
            dt.Columns.Add("IP", typeof(string));
            dt.Columns.Add("Url", typeof(string));
            dt.Columns.Add("Payload", typeof(string));
            dt.Columns.Add("PrevHash", typeof(string));
            dt.Columns.Add("CurrentHash", typeof(string));
            dt.Columns.Add("AlertId", typeof(string));
            dt.Columns.Add("Fingerprint", typeof(string));

            foreach (var item in items)
            {
                var row = dt.NewRow();
                row["Timestamp"] = item.Timestamp;
                row["CreatedAt"] = DateTimeOffset.Now;
                row["Message"] = item.RenderMessage();
                row["Level"] = item.Level.ToString();
                row["Exception"] = item.Exception?.ToString();

                // 1. 處理 TenantId 與 TenantName
                var tIdVal = item.Properties.TryGetValue("TenantId", out var tId) ? tId.ToString()?.Trim('"') : null;
                row["TenantId"] = string.IsNullOrEmpty(tIdVal) ? DBNull.Value : tIdVal;

                if (!string.IsNullOrEmpty(tIdVal) && tenantMap.TryGetValue(tIdVal, out var tName))
                {
                    row["TenantName"] = tName;
                }
                else
                {
                    row["TenantName"] = DBNull.Value;
                }

                // 2. 處理 UserId 與真實中文 UserName
                var uIdVal = item.Properties.TryGetValue("UserId", out var uId) ? uId.ToString()?.Trim('"') : null;
                var unVal = item.Properties.TryGetValue("UserName", out var un) ? un.ToString()?.Trim('"') : null;

                row["UserId"] = string.IsNullOrEmpty(uIdVal) ? DBNull.Value : uIdVal;

                if (!string.IsNullOrEmpty(uIdVal) && userMap.TryGetValue(uIdVal, out var realName))
                {
                    // 優先使用從資料庫查到的真實中文姓名
                    row["UserName"] = realName;
                }
                else if (!string.IsNullOrEmpty(unVal) && unVal != "System" && unVal != "SYSTEM")
                {
                    row["UserName"] = unVal;
                }
                else if (!string.IsNullOrEmpty(uIdVal) && uIdVal != "System" && uIdVal != "SYSTEM")
                {
                    row["UserName"] = uIdVal; // 若查無對應名稱則退回顯示 UserId
                }
                else
                {
                    row["UserName"] = "系統行程";
                }

                row["ModuleName"] = item.Properties.TryGetValue("ModuleName", out var mName) ? mName.ToString()?.Trim('"') : DBNull.Value;
                row["Operation"] = item.Properties.TryGetValue("Operation", out var op) ? op.ToString()?.Trim('"') : DBNull.Value;
                row["OperationDesc"] = item.Properties.TryGetValue("OperationDesc", out var od) ? od.ToString()?.Trim('"') : DBNull.Value;
                row["CorrelationId"] = item.Properties.TryGetValue("CorrelationId", out var cId) ? cId.ToString()?.Trim('"') : DBNull.Value;
                row["IP"] = item.Properties.TryGetValue("IP", out var ip) ? ip.ToString()?.Trim('"') : DBNull.Value;
                row["Url"] = item.Properties.TryGetValue("Url", out var url) ? url.ToString()?.Trim('"') : DBNull.Value;

                row["Payload"] = Extensions.LogEventPropertySerializer.Serialize(item.Properties);

                row["PrevHash"] = DBNull.Value;
                row["CurrentHash"] = DBNull.Value;
                row["AlertId"] = item.Properties.TryGetValue("AlertId", out var aId) ? aId.ToString()?.Trim('"') : DBNull.Value;
                row["Fingerprint"] = item.Properties.TryGetValue("Fingerprint", out var fp) ? fp.ToString()?.Trim('"') : DBNull.Value;

                dt.Rows.Add(row);
            }

            return dt;
        }

        private static void ConfigureColumnMappings(SqlBulkCopy bulkCopy)
        {
            bulkCopy.ColumnMappings.Clear();
            bulkCopy.ColumnMappings.Add("Timestamp", "TimeStamp");
            bulkCopy.ColumnMappings.Add("CreatedAt", "CreatedAt");
            bulkCopy.ColumnMappings.Add("Message", "Message");
            bulkCopy.ColumnMappings.Add("Level", "Level");
            bulkCopy.ColumnMappings.Add("Exception", "Exception");
            bulkCopy.ColumnMappings.Add("TenantId", "TenantId");
            bulkCopy.ColumnMappings.Add("TenantName", "TenantName");     // 💡 對應新欄位
            bulkCopy.ColumnMappings.Add("UserId", "UserId");
            bulkCopy.ColumnMappings.Add("UserName", "UserName");
            bulkCopy.ColumnMappings.Add("ModuleName", "ModuleName");
            bulkCopy.ColumnMappings.Add("Operation", "Operation");
            bulkCopy.ColumnMappings.Add("OperationDesc", "OperationDesc");
            bulkCopy.ColumnMappings.Add("CorrelationId", "CorrelationId");
            bulkCopy.ColumnMappings.Add("IP", "IP");
            bulkCopy.ColumnMappings.Add("Url", "Url");
            bulkCopy.ColumnMappings.Add("Payload", "Payload");
            bulkCopy.ColumnMappings.Add("AlertId", "AlertId");
            bulkCopy.ColumnMappings.Add("Fingerprint", "Fingerprint");
        }

        // 輔助 DTO 結構
        private sealed class UserLookupDto
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
        }

        private sealed class TenantLookupDto
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
        }
    }
}