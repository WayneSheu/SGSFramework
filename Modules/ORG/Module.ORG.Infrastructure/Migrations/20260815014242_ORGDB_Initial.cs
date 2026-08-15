using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGS.Modules.ORG.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ORGDB_Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "org");

            migrationBuilder.Sql(@"
            BEGIN TRANSACTION;

            -- 1. 建立 Schema (若不存在)
            IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'org')
            BEGIN
                EXEC('CREATE SCHEMA [org] AUTHORIZATION [dbo];');
            END

            -- 2. 建立 Append-Only Ledger Table
            IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[org].[AuditLogs]') AND type IN (N'U'))
            BEGIN
                CREATE TABLE [org].[AuditLogs]
                (
                    [Id]                 BIGINT IDENTITY(1,1) NOT NULL,
                    [TraceId]            CHAR(32)             NOT NULL,
                    [UserId]             NVARCHAR(128)        NULL,
                    [RemoteIp]           NVARCHAR(64)         NULL,
                    [CreatedAt]           DATETIMEOFFSET(7)          NOT NULL CONSTRAINT [DF_AuditLogs_CreatedAt] DEFAULT (SYSUTCDATETIME()),
                    [Timestamp]           DATETIMEOFFSET(7)          NOT NULL CONSTRAINT [DF_AuditLogs_Timestamp] DEFAULT (SYSUTCDATETIME()),
                    [Schema]             NVARCHAR(64)         NULL,
                    [TableName]          NVARCHAR(128)        NOT NULL,
                    [Action]             NVARCHAR(50)         NOT NULL,
                    [KeyValues]          NVARCHAR(MAX)        NULL,
                    [OldValues]          NVARCHAR(MAX)        NULL,
                    [NewValues]          NVARCHAR(MAX)        NULL,
                    [ChangedColumns]     NVARCHAR(MAX)        NULL,
                    [PreviousHash]       NVARCHAR(128)        NOT NULL,
                    [StoredHash]         NVARCHAR(128)        NOT NULL,
                    [IsRepaired]         BIT                  NOT NULL CONSTRAINT [DF_AuditLogs_IsRepaired] DEFAULT (0),
                    [RepairedAt]         DATETIMEOFFSET(7)           NULL,
                    [GapReason]          NVARCHAR(500)        NULL,
                    [OriginalStoredHash] NVARCHAR(128)        NULL,

                    -- 複合主鍵 (搭配 CreatedAt 以支援分區與系統時間區間查詢)
                    CONSTRAINT [PK_AuditLogs] PRIMARY KEY CLUSTERED ([Id] ASC, [CreatedAt] ASC)
                )
                WITH
                (
                    -- 啟用 MSSQL Ledger 嚴格防篡改機制 (Append-Only 模式禁止 UPDATE/DELETE)
                    LEDGER = ON (APPEND_ONLY = ON)
                );
            END

            -- 3. 建立全鏈路追蹤涵蓋索引 (IX_AuditLog_TraceId_Covering)
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_AuditLog_TraceId_Covering' AND object_id = OBJECT_ID(N'[org].[AuditLogs]'))
            BEGIN
                CREATE NONCLUSTERED INDEX [IX_AuditLog_TraceId_Covering] 
                ON [org].[AuditLogs] ([TraceId] ASC)
                INCLUDE ([Action], [CreatedAt], [TableName]);
            END

            -- 4. 建立待修復自癒過濾索引 (IX_AuditLog_IsRepaired)
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_AuditLog_IsRepaired' AND object_id = OBJECT_ID(N'[org].[AuditLogs]'))
            BEGIN
                CREATE NONCLUSTERED INDEX [IX_AuditLog_IsRepaired] 
                ON [org].[AuditLogs] ([IsRepaired] ASC)
                INCLUDE ([TableName], [TraceId], [GapReason])
                WHERE [IsRepaired] = 0;
            END

            COMMIT TRANSACTION;");


            migrationBuilder.CreateTable(
                name: "Organization",
                schema: "org",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParentId = table.Column<int>(type: "int", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TenantLabId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Location = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    NodePath = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organization", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "org",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CausationId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Type = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OccurredOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ScheduledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    IsDead = table.Column<bool>(type: "bit", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Organization_Code",
                schema: "org",
                table: "Organization",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Organization_IsDeleted",
                schema: "org",
                table: "Organization",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Organization_NodePath",
                schema: "org",
                table: "Organization",
                column: "NodePath");

            migrationBuilder.CreateIndex(
                name: "IX_Organization_TenantLabId",
                schema: "org",
                table: "Organization",
                column: "TenantLabId",
                filter: "[TenantLabId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_CorrelationId",
                schema: "org",
                table: "OutboxMessages",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_FetchPending",
                schema: "org",
                table: "OutboxMessages",
                columns: new[] { "ProcessedOnUtc", "IsDead", "ScheduledAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs",
                schema: "org");

            migrationBuilder.DropTable(
                name: "Organization",
                schema: "org");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "org");
        }
    }
}
