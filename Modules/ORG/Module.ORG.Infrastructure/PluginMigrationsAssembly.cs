using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Internal;
using System.Reflection;

namespace SGS.Modules.ORG.Infrastructure.Migrations;

/// <summary>
/// 針對動態 ALC 外掛隔離載入環境設計的 IMigrationsAssembly，強制關聯類別與 ModelSnapshot
/// </summary>
#pragma warning disable EF1001 // Internal EF Core API usage
public class PluginMigrationsAssembly : MigrationsAssembly
{
    private readonly Assembly _targetAssembly;

    public PluginMigrationsAssembly(
        ICurrentDbContext currentContext,
        IDbContextOptions options,
        IMigrationsIdGenerator idGenerator,
        IDiagnosticsLogger<DbLoggerCategory.Migrations> logger)
        : base(currentContext, options, idGenerator, logger)
    {
        _targetAssembly = typeof(Dbcontexts.ORGDbContext).Assembly;
    }

    public override IReadOnlyDictionary<string, TypeInfo> Migrations
    {
        get
        {
            var baseMigrations = base.Migrations;
            if (baseMigrations != null && baseMigrations.Count > 0)
            {
                return baseMigrations;
            }

            var result = new Dictionary<string, TypeInfo>();
            var types = _targetAssembly.GetTypes()
                .Where(t => typeof(Migration).IsAssignableFrom(t)
                         && t.GetCustomAttributes(typeof(MigrationAttribute), false).Length > 0);

            foreach (var type in types)
            {
                var attribute = type.GetCustomAttribute<MigrationAttribute>();
                if (attribute != null && !result.ContainsKey(attribute.Id))
                {
                    result.Add(attribute.Id, type.GetTypeInfo());
                }
            }

            return result;
        }
    }

    public override ModelSnapshot? ModelSnapshot
    {
        get
        {
            var baseSnapshot = base.ModelSnapshot;
            if (baseSnapshot != null)
            {
                return baseSnapshot;
            }

            // 反射尋找組件中繼承自 ModelSnapshot 的 SnapShot 類別
            var snapshotType = _targetAssembly.GetTypes()
                .FirstOrDefault(t => typeof(ModelSnapshot).IsAssignableFrom(t));

            if (snapshotType == null)
            {
                return null;
            }

            return (ModelSnapshot)Activator.CreateInstance(snapshotType)!;
        }
    }
}
#pragma warning restore EF1001