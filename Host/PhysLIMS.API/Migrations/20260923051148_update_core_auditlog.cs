using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class update_core_auditlog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- ==========================================
-- 企業級 SQL Server Ledger Table 腳本
-- 適用實體: SGSFramework.Core.Abstractions.Entities.AuditLogs.AuditLogEntity
-- 特性: 防篡改 (Append-Only Ledger Table) + 組合主鍵與覆蓋索引 Optimization
-- ==========================================

-- 1. 建立 Schema（若不存在，預設 core）
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'core')
BEGIN
    EXEC(N'CREATE SCHEMA [core];');
END
GO

-- 2. 建立防篡改 Ledger 資料表 (Append-Only Ledger Table)
IF NOT EXISTS (
    SELECT 1 
    FROM sys.tables t 
    JOIN sys.schemas s ON t.schema_id = s.schema_id 
    WHERE s.name = N'core' AND t.name = N'AuditLogs'
)
BEGIN
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

        -- 設定 (Id, CreatedAt) 組合主鍵以配合 EF Core Entity Configuration
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY CLUSTERED ([Id] ASC, [CreatedAt] ASC)
    )
    WITH
    (
        SYSTEM_VERSIONING = OFF,
        LEDGER = ON (LEDGER_VIEW = [core].[AuditLogs_LedgerView], APPEND_ONLY = ON)
    );
END
GO

-- 3. 建立涵蓋索引 (IX_AuditLog_TraceId_Covering)
IF NOT EXISTS (
    SELECT 1 
    FROM sys.indexes 
    WHERE name = N'IX_AuditLog_TraceId_Covering' 
      AND object_id = OBJECT_ID(N'[core].[AuditLogs]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AuditLog_TraceId_Covering]
    ON [core].[AuditLogs] ([TraceId] ASC)
    INCLUDE ([Action], [CreatedAt], [TableName]);
END
GO

-- 4. 建立過濾索引 (IX_AuditLog_IsRepaired)
IF NOT EXISTS (
    SELECT 1 
    FROM sys.indexes 
    WHERE name = N'IX_AuditLog_IsRepaired' 
      AND object_id = OBJECT_ID(N'[core].[AuditLogs]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AuditLog_IsRepaired]
    ON [core].[AuditLogs] ([IsRepaired] ASC)
    INCLUDE ([TableName], [TraceId], [GapReason])
    WHERE ([IsRepaired] = (0));
END
GO
           ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
