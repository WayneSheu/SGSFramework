using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.SqlServer.Migrations.Internal;
using Microsoft.EntityFrameworkCore.Update;
using System;
using System.Linq;

namespace SGSFramework.Core.Migrations;

#pragma warning disable EF1001 // Internal EF Core API usage
public class CustomSqlServerMigrationsSqlGenerator : SqlServerMigrationsSqlGenerator
{
    public CustomSqlServerMigrationsSqlGenerator(
        MigrationsSqlGeneratorDependencies dependencies,
        ICommandBatchPreparer commandBatchPreparer)
        : base(dependencies, commandBatchPreparer)
    {
    }

    protected override void Generate(
        CreateTableOperation operation,
        IModel? model,
        MigrationCommandListBuilder builder,
        bool terminate = true)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(builder);

        bool isLedger = false;

        // 1. 先從 Operation 檢查，若無則直接從 Model 尋找實體的 Ledger 註解
        if (operation.FindAnnotation("SqlServer:IsLedgerAppendOnly")?.Value is bool appendOnly && appendOnly)
        {
            isLedger = true;
        }
        else if (model != null)
        {
            var defaultSchema = model.GetDefaultSchema();
            var targetSchema = operation.Schema ?? defaultSchema;

            var entityType = model.GetEntityTypes().FirstOrDefault(e =>
                string.Equals(e.GetTableName(), operation.Name, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(e.GetSchema() ?? defaultSchema, targetSchema, StringComparison.OrdinalIgnoreCase));

            if (entityType != null)
            {
                var annotation = entityType.FindAnnotation("SqlServer:IsLedgerAppendOnly");
                if (annotation?.Value is bool isAppendOnlyVal && isAppendOnlyVal)
                {
                    isLedger = true;
                }
            }
        }

        // 2. 若為 Ledger Table 則拼接 SQL
        if (isLedger)
        {
            base.Generate(operation, model, builder, terminate: false);
            builder.AppendLine();
            builder.Append("WITH (LEDGER = ON (APPEND_ONLY = ON))");

            if (terminate)
            {
                builder.AppendLine(";");
                EndStatement(builder);
            }
        }
        else
        {
            base.Generate(operation, model, builder, terminate);
        }
    }
}
#pragma warning restore EF1001