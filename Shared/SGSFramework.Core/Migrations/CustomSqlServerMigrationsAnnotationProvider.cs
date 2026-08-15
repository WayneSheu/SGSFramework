using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.SqlServer.Metadata.Internal;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SGSFramework.Core.Migrations;

#pragma warning disable EF1001
public class CustomSqlServerAnnotationProvider : SqlServerAnnotationProvider
{
    public CustomSqlServerAnnotationProvider(RelationalAnnotationProviderDependencies dependencies)
        : base(dependencies)
    {
    }

    public override IEnumerable<IAnnotation> For(ITable table, bool designTime)
    {
        ArgumentNullException.ThrowIfNull(table);

        // 1. 保留 EF Core 內建的 Table 級別標記
        foreach (var annotation in base.For(table, designTime))
        {
            yield return annotation;
        }

        var entityType = table.EntityTypeMappings.FirstOrDefault()?.TypeBase;
        if (entityType != null)
        {
            // 2. 修正篩選條件：加入對 "SqlServer:IsLedger" 或明確名稱的匹配
            var customAnnotations = entityType.GetAnnotations()
                .Where(a => a.Name.StartsWith("SqlServer:IsLedger", StringComparison.OrdinalIgnoreCase) ||
                            a.Name.StartsWith("SqlServer:Ledger", StringComparison.OrdinalIgnoreCase) ||
                            a.Name.StartsWith("Custom:", StringComparison.OrdinalIgnoreCase));

            foreach (var customAnnotation in customAnnotations)
            {
                yield return customAnnotation;
            }
        }
    }
}
#pragma warning restore EF1001