using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    public partial class update_core_auditlog : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            try
            {
                migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'core')
BEGIN
    EXEC(N'CREATE SCHEMA [core];');
END

-- 1. 若已存在舊的 AuditLogs (無論是否為 Ledger Table)，先清理視圖與資料表
IF EXISTS (
    SELECT 1 
    FROM sys.tables t 
    JOIN sys.schemas s ON t.schema_id = s.schema_id 
    WHERE s.name = N'core' AND t.name = N'AuditLogs'
)
BEGIN
    -- 移除舊索引
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditLog_IsRepaired' AND object_id = OBJECT_ID(N'[core].[AuditLogs]'))
        DROP INDEX [IX_AuditLog_IsRepaired] ON [core].[AuditLogs];

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditLog_TraceId_Covering' AND object_id = OBJECT_ID(N'[core].[AuditLogs]'))
        DROP INDEX [IX_AuditLog_TraceId_Covering] ON [core].[AuditLogs];

    DROP TABLE [core].[AuditLogs];
END

-- 刪除可能殘留的 Ledger View
IF EXISTS (SELECT 1 FROM sys.views v JOIN sys.schemas s ON v.schema_id = s.schema_id WHERE s.name = N'core' AND v.name = N'AuditLogs_LedgerView')
BEGIN
    DROP VIEW [core].[AuditLogs_LedgerView];
END

-- 2. 強制建立為 Append-Only Ledger Table
CREATE TABLE [core].[AuditLogs]
(
    [Id]                 BIGINT IDENTITY(1,1) NOT NULL,
    [TraceId]            VARCHAR(64)          NOT NULL,
    [UserId]             NVARCHAR(128)        NULL,
    [RemoteIp]           NVARCHAR(64)         NULL,
    [CreatedAt]          DATETIMEOFFSET(7)    NOT NULL DEFAULT (SYSDATETIMEOFFSET()),
    [Timestamp]          DATETIMEOFFSET(7)    NOT NULL DEFAULT (SYSDATETIMEOFFSET()),
    [Schema]             NVARCHAR(64)         NULL,
    [TableName]          NVARCHAR(128)        NOT NULL,
    [Action]             NVARCHAR(50)         NOT NULL,
    [KeyValues]          NVARCHAR(MAX)        NULL,
    [OldValues]          NVARCHAR(MAX)        NULL,
    [NewValues]          NVARCHAR(MAX)        NULL,
    [ChangedColumns]     NVARCHAR(MAX)        NULL,
    [PreviousHash]       NVARCHAR(128)        NOT NULL,
    [StoredHash]         NVARCHAR(128)        NOT NULL,
    [IsRepaired]         BIT                  NOT NULL DEFAULT (0),
    [RepairedAt]         DATETIMEOFFSET(7)    NULL,
    [GapReason]          NVARCHAR(500)        NULL,
    [OriginalStoredHash] NVARCHAR(128)        NULL,

    CONSTRAINT [PK_AuditLogs] PRIMARY KEY CLUSTERED ([Id] ASC, [CreatedAt] ASC)
)
WITH
(
    SYSTEM_VERSIONING = OFF,
    LEDGER = ON (LEDGER_VIEW = [core].[AuditLogs_LedgerView], APPEND_ONLY = ON)
);

-- 3. 建立覆蓋索引
CREATE NONCLUSTERED INDEX [IX_AuditLog_TraceId_Covering]
ON [core].[AuditLogs] ([TraceId] ASC)
INCLUDE ([Action], [CreatedAt], [TableName]);

CREATE NONCLUSTERED INDEX [IX_AuditLog_IsRepaired]
ON [core].[AuditLogs] ([IsRepaired] ASC)
INCLUDE ([TableName], [TraceId], [GapReason])
WHERE ([IsRepaired] = 0);
");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("執行 Migration 20260923051148_update_core_auditlog Up 失敗。", ex);
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = N'core' AND t.name = N'AuditLogs')
BEGIN
    DROP TABLE [core].[AuditLogs];
END
");
        }
    }
}