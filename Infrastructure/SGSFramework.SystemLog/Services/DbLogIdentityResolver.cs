#nullable enable
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Logging;

namespace SGSFramework.SystemLog.Services
{
    /// <summary>以主庫 DbContext 查 core.AspNetUsers / org.Organization，供外掛日誌庫寫入時補顯示名稱。</summary>
    public sealed class DbLogIdentityResolver : ILogIdentityResolver
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<DbLogIdentityResolver> _logger;

        public DbLogIdentityResolver(IServiceScopeFactory scopeFactory, ILogger<DbLogIdentityResolver> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task<(IReadOnlyDictionary<string, string> Users, IReadOnlyDictionary<string, string> Tenants)> ResolveAsync(
            IReadOnlyCollection<Guid> userIds, IReadOnlyCollection<string> tenantIds, CancellationToken cancellationToken)
        {
            var users = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var tenants = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (userIds.Count == 0 && tenantIds.Count == 0) return (users, tenants);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetService<DbContext>();
                if (db is null) return (users, tenants);

                if (userIds.Count > 0)
                {
                    var ps = userIds.Select((id, i) => new SqlParameter($"@p{i}", id)).ToArray();
                    var sql = $"SELECT CAST(Id AS NVARCHAR(36)) AS Id, COALESCE(UserName, FullName, CAST(Id AS NVARCHAR(36))) AS Name FROM core.AspNetUsers WHERE Id IN ({string.Join(", ", ps.Select(p => p.ParameterName))})";
                    foreach (var u in await db.Database.SqlQueryRaw<Lookup>(sql, ps.Cast<object>().ToArray()).ToListAsync(cancellationToken))
                        if (!string.IsNullOrEmpty(u.Id) && !string.IsNullOrEmpty(u.Name)) users[u.Id] = u.Name;
                }

                if (tenantIds.Count > 0)
                {
                    var ps = tenantIds.Select((id, i) => new SqlParameter($"@t{i}", id)).ToArray();
                    var sql = $"SELECT CAST(Id AS NVARCHAR(36)) AS Id, COALESCE(Name, Id) AS Name FROM org.Organization WHERE Id IN ({string.Join(", ", ps.Select(p => p.ParameterName))})";
                    foreach (var t in await db.Database.SqlQueryRaw<Lookup>(sql, ps.Cast<object>().ToArray()).ToListAsync(cancellationToken))
                        if (!string.IsNullOrEmpty(t.Id) && !string.IsNullOrEmpty(t.Name)) tenants[t.Id] = t.Name;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "[LogIdentityResolver] 解析使用者/實驗室名稱失敗，將採用降級文字。");
            }

            return (users, tenants);
        }

        public async Task<IReadOnlyList<string>> FindLoginNamesAsync(string keyword, int maxCount, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(keyword)) return Array.Empty<string>();

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetService<DbContext>();
                if (db is null) return Array.Empty<string>();

                // LIKE 萬用字元（% _ [）以 \ 跳脫，避免關鍵字被當成樣式
                var escaped = keyword.Trim()
                    .Replace(@"\", @"\\")
                    .Replace("%", @"\%")
                    .Replace("_", @"\_")
                    .Replace("[", @"\[");

                var sql = $"SELECT TOP ({Math.Clamp(maxCount, 1, 500)}) UserName AS Id, COALESCE(FullName, UserName) AS Name " +
                          @"FROM core.AspNetUsers WHERE UserName IS NOT NULL AND (FullName LIKE @k ESCAPE '\' OR UserName LIKE @k ESCAPE '\')";

                var rows = await db.Database
                    .SqlQueryRaw<Lookup>(sql, new SqlParameter("@k", "%" + escaped + "%"))
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return rows.Select(r => r.Id).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "[LogIdentityResolver] 依姓名查詢使用者帳號失敗。");
                return Array.Empty<string>();
            }
        }

        public async Task<IReadOnlyDictionary<string, string>> ResolveDisplayNamesAsync(
            IReadOnlyCollection<string> loginNames, CancellationToken cancellationToken)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (loginNames.Count == 0) return map;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetService<DbContext>();
                if (db is null) return map;

                var ps = loginNames.Select((n, i) => new SqlParameter($"@u{i}", n)).ToArray();
                var sql = "SELECT UserName AS Id, COALESCE(FullName, UserName) AS Name FROM core.AspNetUsers " +
                          $"WHERE UserName IN ({string.Join(", ", ps.Select(p => p.ParameterName))})";

                foreach (var r in await db.Database.SqlQueryRaw<Lookup>(sql, ps.Cast<object>().ToArray())
                             .ToListAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (!string.IsNullOrEmpty(r.Id) && !string.IsNullOrEmpty(r.Name)) map[r.Id] = r.Name;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "[LogIdentityResolver] 解析人員姓名失敗，將顯示帳號。");
            }

            return map;
        }

        private sealed class Lookup
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
        }
    }
}
