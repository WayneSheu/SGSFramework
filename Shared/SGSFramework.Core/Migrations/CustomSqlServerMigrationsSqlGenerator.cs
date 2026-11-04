// ==========================================
// 檔案路徑: src/SGSFramework.Core/Migrations/CustomSqlServerMigrationsSqlGenerator.cs
// 架構層級: Infrastructure / Migrations
// ==========================================

#nullable enable
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

    // sp_addextendedproperty 的 @level*name 為 sysname 參數，須為字串常值而非 [識別碼]
    private static string NLiteral(string value) => "N'" + value.Replace("'", "''") + "'";

    protected override void Generate(
        CreateTableOperation operation,
        IModel? model,
        MigrationCommandListBuilder builder,
        bool terminate = true)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(builder);

        bool isLedger = false;

        // 1. 從 Operation 或 Model 檢查是否為 Ledger Table
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
            // 暫存並清除 Comment，避免 MigrationCommandListBuilder 拋出帶有註解的未終止 SQL 例外
            var originalComment = operation.Comment;
            operation.Comment = null;

            // 欄位註解同樣會觸發「未終止 SQL 不可含註解」例外：暫存清除，建表後以 sp_addextendedproperty 補上
            var columnComments = operation.Columns
                .Where(c => !string.IsNullOrEmpty(c.Comment))
                .Select(c => (Column: c, Comment: c.Comment!))
                .ToList();
            foreach (var (column, _) in columnComments) column.Comment = null;

            base.Generate(operation, model, builder, terminate: false);

            // 恢復註解
            operation.Comment = originalComment;
            foreach (var (column, comment) in columnComments) column.Comment = comment;

            builder.AppendLine();
            builder.Append("WITH (LEDGER = ON (APPEND_ONLY = ON))");

            if (terminate)
            {
                builder.AppendLine(";");
                EndStatement(builder);
            }

            // 🟢 若原本帶有 Table Comment，手動以 sp_addextendedproperty 產生 SQL Server 資料表註解
            if (!string.IsNullOrEmpty(originalComment))
            {
                var schemaName = operation.Schema ?? model?.GetDefaultSchema() ?? "core";
                var tableName = operation.Name;

                builder.AppendLine();
                builder.Append("EXEC sys.sp_addextendedproperty ");
                builder.Append("@name=N'MS_Description', ");
                builder.Append("@value=N'");
                builder.Append(originalComment.Replace("'", "''"));
                builder.Append("', ");
                builder.Append("@level0type=N'SCHEMA', @level0name=");
                // 修正：透過 Dependencies.SqlGenerationHelper 呼叫
                builder.Append(NLiteral(schemaName));
                builder.Append(", ");
                builder.Append("@level1type=N'TABLE', @level1name=");
                builder.Append(NLiteral(tableName));
                builder.Append(";");

                if (terminate)
                {
                    builder.AppendLine();
                    EndStatement(builder);
                }
            }

            // 欄位註解：逐欄位補 sp_addextendedproperty（僅在 terminate 時產生，與資料表註解一致）
            if (terminate)
            {
                var schemaName = operation.Schema ?? model?.GetDefaultSchema() ?? "core";
                foreach (var (column, comment) in columnComments)
                {
                    builder.Append("EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'");
                    builder.Append(comment.Replace("'", "''"));
                    builder.Append("', @level0type=N'SCHEMA', @level0name=");
                    builder.Append(NLiteral(schemaName));
                    builder.Append(", @level1type=N'TABLE', @level1name=");
                    builder.Append(NLiteral(operation.Name));
                    builder.Append(", @level2type=N'COLUMN', @level2name=");
                    builder.Append(NLiteral(column.Name));
                    builder.Append(";");
                    builder.AppendLine();
                    EndStatement(builder);
                }
            }
        }
        else
        {
            base.Generate(operation, model, builder, terminate);
        }
    }
}
#pragma warning restore EF1001