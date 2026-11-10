#nullable enable
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace SGSFramework.Core.Logging
{
    /// <summary>日誌資料庫分層項目：熱庫（現行、可寫）或冷庫（年度換庫後的唯讀歷史庫）。</summary>
    /// <param name="Key">來源識別：熱庫固定為 <see cref="LogTierCatalog.HotKey"/>，冷庫為 cold-{設定的 Key}</param>
    /// <param name="DisplayName">畫面顯示名稱</param>
    /// <param name="Database">資料庫名稱</param>
    /// <param name="IsHot">是否為熱庫</param>
    public sealed record LogTierInfo(string Key, string DisplayName, string Database, bool IsHot);

    /// <summary>
    /// 日誌庫冷熱分層目錄。熱庫由 <c>LogConnection</c> 指定；冷庫由設定 <c>LogArchive:Databases</c> 列出，
    /// 並（預設）自動偵測同一伺服器上名為 <c>{熱庫}_{年度}</c>（或半年度 <c>_2026H1</c> / <c>_2026H2</c>）且為 READ_ONLY 的資料庫，年度換庫完成後不需改設定或重啟。
    /// 連線字串以 <c>LogConnection</c> 為基礎僅替換資料庫名稱（不另存密碼）。
    /// </summary>
    public interface ILogTierCatalog
    {
        LogTierInfo Hot { get; }

        /// <summary>冷庫清單（設定順序在前、自動偵測依年度由舊到新；越後面越新，寫入雜湊鏈銜接時由新到舊尋找）</summary>
        IReadOnlyList<LogTierInfo> Colds { get; }

        /// <summary>熱庫 + 冷庫</summary>
        IReadOnlyList<LogTierInfo> All { get; }

        /// <summary>設定有誤而被略過的冷庫項目說明（供啟動時記錄警告）</summary>
        IReadOnlyList<string> Problems { get; }

        /// <summary>取得指定來源的連線字串；空值視為熱庫。未知來源擲出 <see cref="ArgumentException"/>。</summary>
        string GetConnectionString(string? key);

        /// <summary>解析來源識別；空值回傳熱庫，未知來源回傳 null。</summary>
        LogTierInfo? Find(string? key);

        /// <summary>重新讀取設定並偵測冷庫（啟動時與年度換庫完成後呼叫）。偵測失敗僅記入 <see cref="Problems"/>，保留設定的冷庫。</summary>
        void Refresh();
    }

    public sealed class LogTierCatalog : ILogTierCatalog
    {
        public const string HotKey = "hot";
        public const string ColdKeyPrefix = "cold-";
        public const string ArchiveSection = "LogArchive:Databases";
        public const string AutoDiscoverKey = "LogArchive:AutoDiscover";
        public const string RuntimeConnectionKey = "PersistentSettings:ConnectionStrings:LogConnection";

        private static readonly Regex KeyPattern = new("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex DatabasePattern = new(@"^[A-Za-z0-9_\-\.]{1,128}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private readonly IConfiguration _configuration;
        private readonly string _baseConnection;
        private readonly string _hotDatabase;
        private volatile Snapshot _snapshot;

        private sealed record Snapshot(
            IReadOnlyList<LogTierInfo> Colds,
            IReadOnlyList<LogTierInfo> All,
            IReadOnlyList<string> Problems,
            IReadOnlyDictionary<string, LogTierInfo> ByKey);

        public LogTierCatalog(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            _configuration = configuration;

            _baseConnection = configuration[RuntimeConnectionKey]
                ?? throw new InvalidOperationException($"未設定日誌資料庫連線：{RuntimeConnectionKey}");

            _hotDatabase = new SqlConnectionStringBuilder(_baseConnection).InitialCatalog;
            Hot = new LogTierInfo(HotKey, "熱資料（現行）", _hotDatabase, IsHot: true);

            // 建構時僅讀設定（不連線資料庫）；自動偵測由 Refresh() 明確觸發
            _snapshot = Build(Array.Empty<string>(), discoveryProblem: null);
        }

        public LogTierInfo Hot { get; }

        public IReadOnlyList<LogTierInfo> Colds => _snapshot.Colds;

        public IReadOnlyList<LogTierInfo> All => _snapshot.All;

        public IReadOnlyList<string> Problems => _snapshot.Problems;

        public LogTierInfo? Find(string? key) =>
            string.IsNullOrWhiteSpace(key) ? Hot : (string.Equals(key.Trim(), HotKey, StringComparison.OrdinalIgnoreCase) ? Hot : _snapshot.ByKey.GetValueOrDefault(key.Trim()));

        public string GetConnectionString(string? key)
        {
            var tier = Find(key) ?? throw new ArgumentException($"找不到日誌資料庫來源：{key}", nameof(key));
            if (tier.IsHot) return _baseConnection;

            return new SqlConnectionStringBuilder(_baseConnection) { InitialCatalog = tier.Database }.ConnectionString;
        }

        public void Refresh()
        {
            var discovered = Array.Empty<string>();
            string? problem = null;

            if (_configuration.GetValue("LogArchive:AutoDiscover", true))
            {
                try
                {
                    discovered = DiscoverColdDatabases();
                }
                catch (Exception ex)
                {
                    problem = $"自動偵測冷庫失敗（仍使用 {ArchiveSection} 設定的冷庫）：{ex.Message}";
                }
            }

            _snapshot = Build(discovered, problem);
        }

        /// <summary>查詢同一伺服器上名為 {熱庫}_{年度 / 年度H1 / 年度H2}、可連線且為 READ_ONLY 的資料庫（換庫中仍在還原的庫不會出現）</summary>
        private string[] DiscoverColdDatabases()
        {
            var builder = new SqlConnectionStringBuilder(_baseConnection) { InitialCatalog = "master", ConnectTimeout = 10 };

            using var connection = new SqlConnection(builder.ConnectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT name FROM sys.databases WHERE name LIKE @pattern ESCAPE '\\' AND is_read_only = 1 AND state = 0 ORDER BY name;";
            command.CommandTimeout = 15;
            command.Parameters.Add(new SqlParameter("@pattern", System.Data.SqlDbType.NVarChar, 300)
            {
                Value = LogRotationNaming.ColdLikePattern(_hotDatabase)
            });

            var names = new List<string>();
            using var reader = command.ExecuteReader();
            while (reader.Read()) names.Add(reader.GetString(0));
            return names.ToArray();
        }

        private Snapshot Build(IReadOnlyCollection<string> discovered, string? discoveryProblem)
        {
            var byKey = new Dictionary<string, LogTierInfo>(StringComparer.OrdinalIgnoreCase) { [HotKey] = Hot };
            var colds = new List<LogTierInfo>();
            var problems = new List<string>();
            if (discoveryProblem is not null) problems.Add(discoveryProblem);

            var index = 0;
            foreach (var child in _configuration.GetSection(ArchiveSection).GetChildren())
            {
                index++;
                var key = child["Key"]?.Trim();
                var database = child["Database"]?.Trim();

                if (string.IsNullOrEmpty(key) || !KeyPattern.IsMatch(key))
                {
                    problems.Add($"{ArchiveSection}[{index}]：Key 須為小寫英數並可用連字號分隔（目前：'{key}'）。");
                    continue;
                }

                if (string.IsNullOrEmpty(database) || !DatabasePattern.IsMatch(database))
                {
                    problems.Add($"{ArchiveSection}[{index}]：Database 名稱不合法（目前：'{database}'）。");
                    continue;
                }

                if (string.Equals(database, _hotDatabase, StringComparison.OrdinalIgnoreCase))
                {
                    problems.Add($"{ArchiveSection}[{index}]：冷庫 '{database}' 不可與熱庫相同。");
                    continue;
                }

                var fullKey = ColdKeyPrefix + key;
                if (byKey.ContainsKey(fullKey))
                {
                    problems.Add($"{ArchiveSection}[{index}]：Key '{key}' 重複。");
                    continue;
                }

                var display = string.IsNullOrWhiteSpace(child["DisplayName"]) ? $"冷資料（{database}）" : child["DisplayName"]!.Trim();
                var info = new LogTierInfo(fullKey, display, database, IsHot: false);
                colds.Add(info);
                byKey[fullKey] = info;
            }

            // 自動偵測的冷庫（年度 {熱庫}_2026、半年度 {熱庫}_2026H1 / _2026H2）：已由設定列出（同資料庫或同 Key）者以設定為準；
            // 其餘依期間先後由舊到新附加
            var found = new List<(LogRotationPeriod Period, string Database)>();
            foreach (var database in discovered)
            {
                if (LogRotationNaming.TryParseColdPeriod(_hotDatabase, database, out var period)) found.Add((period, database));
            }

            foreach (var (period, database) in found.OrderBy(f => f.Period).ThenBy(f => f.Database, StringComparer.OrdinalIgnoreCase))
            {
                if (colds.Any(c => string.Equals(c.Database, database, StringComparison.OrdinalIgnoreCase))) continue;

                var fullKey = ColdKeyPrefix + period.Key;
                if (byKey.ContainsKey(fullKey)) continue;

                var info = new LogTierInfo(fullKey, $"冷資料 {period.DisplayName}", database, IsHot: false);
                colds.Add(info);
                byKey[fullKey] = info;
            }

            return new Snapshot(colds, new[] { Hot }.Concat(colds).ToList(), problems, byKey);
        }
    }
}
