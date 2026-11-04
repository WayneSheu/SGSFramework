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

        private sealed class Lookup
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
        }
    }
}
