using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using SGSFramework.Core.Configurations;

namespace SGSFramework.Core.Migrations
{
    /// <summary>
    /// 最小權限 (ISO 27001 A.8.2)：執行期 DbContext 一律使用 DefaultConnection (app_sgs_user，僅 DML)，
    /// 僅在套用 Migration 期間暫時切換為 MigrationConnection (deploy_sgs_user，具 DDL)，結束後立即切回。
    /// </summary>
    public static class MigrationConnectionExtensions
    {
        public const string MigrationConnectionKey = PersistentConnectionOptions.SectionName + ":MigrationConnection";

        /// <summary>
        /// 於 using 範圍內改用部署帳號連線；未設定 MigrationConnection 時不切換 (沿用 DefaultConnection)。
        /// 必須在連線關閉時呼叫 (Migration 前尚未開啟連線)，可巢狀呼叫。
        /// </summary>
        public static IDisposable UseMigrationConnection(this DatabaseFacade database, IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(database);
            ArgumentNullException.ThrowIfNull(configuration);

            var migrationConnection = configuration[MigrationConnectionKey];
            var currentConnection = database.GetConnectionString();

            if (string.IsNullOrWhiteSpace(migrationConnection)
                || string.Equals(migrationConnection, currentConnection, StringComparison.Ordinal))
            {
                return NoopScope.Instance;
            }

            database.SetConnectionString(migrationConnection);
            return new RestoreScope(database, currentConnection);
        }

        private sealed class RestoreScope(DatabaseFacade database, string? originalConnection) : IDisposable
        {
            private bool _disposed;

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;

                // 切回前確保連線已關閉，避免同一 Scope 後續的執行期查詢沿用部署帳號連線
                database.CloseConnection();
                database.SetConnectionString(originalConnection);
            }
        }

        private sealed class NoopScope : IDisposable
        {
            public static readonly NoopScope Instance = new();
            public void Dispose() { }
        }
    }
}
