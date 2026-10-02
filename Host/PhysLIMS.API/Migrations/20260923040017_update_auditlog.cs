using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable enable

namespace PhysLIMS.API.Migrations
{
    /// <summary>
    /// 對齊 AuditLogEntity 實體配置之 Migration 檔 (相容於 MSSQL 2025 Ledger 防篡改鏈路與一般資料表之無縫結構轉移)
    /// </summary>
    public partial class update_auditlog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            try
            {
                migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1 
    FROM sys.tables t
    JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = 'core' AND t.name = 'AuditLogs'
)
BEGIN
    -- 1. 清除 Temp 暫存表 (若殘留)
    IF OBJECT_ID(N'[core].[AuditLogs_Temp]', N'U') IS NOT NULL
    BEGIN
        DROP TABLE [core].[AuditLogs_Temp];
    END

    -- 2. 建立新結構之暫存表 (維持 Standard/Ledger 相容 Schema)
    CREATE TABLE [core].[AuditLogs_Temp] (
        [Id] BIGINT IDENTITY(1,1) NOT NULL,
        [Timestamp] DATETIMEOFFSET(7) NOT NULL DEFAULT (SYSDATETIMEOFFSET()),
        [CreatedAt] DATETIMEOFFSET(7) NOT NULL DEFAULT (SYSDATETIMEOFFSET()),
        [Action] NVARCHAR(50) NOT NULL,
        [TableName] NVARCHAR(128) NOT NULL,
        [Schema] NVARCHAR(64) NULL,
        [TraceId] VARCHAR(64) NOT NULL,
        [UserId] NVARCHAR(128) NULL,
        [RemoteIp] NVARCHAR(64) NULL,
        [KeyValues] NVARCHAR(MAX) NULL,
        [OldValues] NVARCHAR(MAX) NULL,
        [NewValues] NVARCHAR(MAX) NULL,
        [ChangedColumns] NVARCHAR(MAX) NULL,
        [PreviousHash] NVARCHAR(128) NOT NULL,
        [StoredHash] NVARCHAR(128) NOT NULL,
        [OriginalStoredHash] NVARCHAR(128) NULL,
        [IsRepaired] BIT NOT NULL DEFAULT (0),
        [RepairedAt] DATETIMEOFFSET(7) NULL,
        [GapReason] NVARCHAR(500) NULL,
        CONSTRAINT [PK_AuditLogs_Temp] PRIMARY KEY CLUSTERED ([Id] ASC, [CreatedAt] ASC)
    );

    -- 3. 轉移歷史資料
    SET IDENTITY_INSERT [core].[AuditLogs_Temp] ON;

    INSERT INTO [core].[AuditLogs_Temp] (
        [Id], [Timestamp], [CreatedAt], [Action], [TableName], [Schema], 
        [TraceId], [UserId], [RemoteIp], [KeyValues], [OldValues], 
        [NewValues], [ChangedColumns], [PreviousHash], [StoredHash], 
        [OriginalStoredHash], [IsRepaired], [RepairedAt], [GapReason]
    )
    SELECT 
        [Id], [Timestamp], [CreatedAt], [Action], [TableName], [Schema], 
        CAST([TraceId] AS VARCHAR(64)), [UserId], [RemoteIp], [KeyValues], [OldValues], 
        [NewValues], [ChangedColumns], [PreviousHash], [StoredHash], 
        [OriginalStoredHash], [IsRepaired], [RepairedAt], [GapReason]
    FROM [core].[AuditLogs];

    SET IDENTITY_INSERT [core].[AuditLogs_Temp] OFF;

    -- 4. 刪除原表並替換
    DROP TABLE [core].[AuditLogs];
    EXEC sp_rename 'core.AuditLogs_Temp', 'AuditLogs';
    EXEC sp_rename 'core.PK_AuditLogs_Temp', 'PK_AuditLogs', 'OBJECT';

    -- 5. 重建索引
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuditLog_IsRepaired' AND object_id = OBJECT_ID(N'[core].[AuditLogs]'))
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_AuditLog_IsRepaired] 
        ON [core].[AuditLogs] ([IsRepaired]) 
        INCLUDE ([TableName], [TraceId], [GapReason]) 
        WHERE [IsRepaired] = 0;
    END

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuditLog_TraceId_Covering' AND object_id = OBJECT_ID(N'[core].[AuditLogs]'))
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_AuditLog_TraceId_Covering] 
        ON [core].[AuditLogs] ([TraceId]) 
        INCLUDE ([Action], [CreatedAt], [TableName]);
    END
END
                ");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("執行 Migration 20260923040017_update_auditlog 失敗。", ex);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            try
            {
                migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1 
    FROM sys.tables t
    JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = 'core' AND t.name = 'AuditLogs'
)
BEGIN
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuditLog_IsRepaired' AND object_id = OBJECT_ID(N'[core].[AuditLogs]'))
    BEGIN
        DROP INDEX [IX_AuditLog_IsRepaired] ON [core].[AuditLogs];
    END

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuditLog_TraceId_Covering' AND object_id = OBJECT_ID(N'[core].[AuditLogs]'))
    BEGIN
        DROP INDEX [IX_AuditLog_TraceId_Covering] ON [core].[AuditLogs];
    END

    IF OBJECT_ID(N'[core].[AuditLogs_Rollback]', N'U') IS NOT NULL
    BEGIN
        DROP TABLE [core].[AuditLogs_Rollback];
    END

    CREATE TABLE [core].[AuditLogs_Rollback] (
        [Id] BIGINT IDENTITY(1,1) NOT NULL,
        [Timestamp] DATETIMEOFFSET(7) NOT NULL DEFAULT (SYSDATETIMEOFFSET()),
        [CreatedAt] DATETIMEOFFSET(7) NOT NULL DEFAULT (SYSDATETIMEOFFSET()),
        [Action] NVARCHAR(50) NOT NULL,
        [TableName] NVARCHAR(128) NOT NULL,
        [Schema] NVARCHAR(64) NULL,
        [TraceId] VARCHAR(32) NOT NULL,
        [UserId] NVARCHAR(128) NULL,
        [RemoteIp] NVARCHAR(64) NULL,
        [KeyValues] NVARCHAR(MAX) NULL,
        [OldValues] NVARCHAR(MAX) NULL,
        [NewValues] NVARCHAR(MAX) NULL,
        [ChangedColumns] NVARCHAR(MAX) NULL,
        [PreviousHash] NVARCHAR(128) NOT NULL,
        [StoredHash] NVARCHAR(128) NOT NULL,
        [OriginalStoredHash] NVARCHAR(128) NULL,
        [IsRepaired] BIT NOT NULL DEFAULT (0),
        [RepairedAt] DATETIMEOFFSET(7) NULL,
        [GapReason] NVARCHAR(500) NULL,
        CONSTRAINT [PK_AuditLogs_Rollback] PRIMARY KEY CLUSTERED ([Id] ASC, [CreatedAt] ASC)
    );

    SET IDENTITY_INSERT [core].[AuditLogs_Rollback] ON;

    INSERT INTO [core].[AuditLogs_Rollback] (
        [Id], [Timestamp], [CreatedAt], [Action], [TableName], [Schema], 
        [TraceId], [UserId], [RemoteIp], [KeyValues], [OldValues], 
        [NewValues], [ChangedColumns], [PreviousHash], [StoredHash], 
        [OriginalStoredHash], [IsRepaired], [RepairedAt], [GapReason]
    )
    SELECT 
        [Id], [Timestamp], [CreatedAt], [Action], [TableName], [Schema], 
        CAST([TraceId] AS VARCHAR(32)), [UserId], [RemoteIp], [KeyValues], [OldValues], 
        [NewValues], [ChangedColumns], [PreviousHash], [StoredHash], 
        [OriginalStoredHash], [IsRepaired], [RepairedAt], [GapReason]
    FROM [core].[AuditLogs];

    SET IDENTITY_INSERT [core].[AuditLogs_Rollback] OFF;

    DROP TABLE [core].[AuditLogs];
    EXEC sp_rename 'core.AuditLogs_Rollback', 'AuditLogs';
    EXEC sp_rename 'core.PK_AuditLogs_Rollback', 'PK_AuditLogs', 'OBJECT';

    CREATE NONCLUSTERED INDEX [IX_AuditLog_IsRepaired] 
    ON [core].[AuditLogs] ([IsRepaired]) 
    INCLUDE ([TableName], [TraceId], [GapReason]) 
    WHERE [IsRepaired] = 0;

    CREATE NONCLUSTERED INDEX [IX_AuditLog_TraceId_Covering] 
    ON [core].[AuditLogs] ([TraceId]) 
    INCLUDE ([Action], [CreatedAt], [TableName]);
END
                ");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("執行 Migration Down 復原作業失敗。", ex);
            }
        }
    }
}