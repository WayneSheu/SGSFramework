using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.SqlServer.Migrations.Internal;
using Microsoft.EntityFrameworkCore.Update;
using System;
using System.Linq;

namespace SGSFramework.Core.Migrations;

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

        // 1. 檢查 Operation 層級 Annotation
        if (operation["SqlServer:IsLedgerAppendOnly"] is bool opAnnotation && opAnnotation)
        {
            isLedger = true;
        }
        // 2. 若 Operation 未攜帶，回溯至 EF Model EntityType 進行比對
        else if (model != null)
        {
            var entityType = model.GetEntityTypes()
                .FirstOrDefault(e => string.Equals(e.GetTableName(), operation.Name, StringComparison.OrdinalIgnoreCase)
                                  && string.Equals(e.GetSchema() ?? model.GetDefaultSchema(), operation.Schema, StringComparison.OrdinalIgnoreCase));

            if (entityType != null)
            {
                var annotation = entityType.FindAnnotation("SqlServer:IsLedgerAppendOnly");
                if (annotation?.Value is bool modelAnnotation && modelAnnotation)
                {
                    isLedger = true;
                }
            }
        }

        if (isLedger)
        {
            // 產生標準 CREATE TABLE SQL，暫不終止語句 (不加分號)
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