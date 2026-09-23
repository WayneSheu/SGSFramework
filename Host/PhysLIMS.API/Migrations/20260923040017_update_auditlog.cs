using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class update_auditlog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 防禦性檢查：確認 Schema 與資料表存在後才進行欄位型態調整
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 
                    FROM sys.tables t
                    JOIN sys.schemas s ON t.schema_id = s.schema_id
                    WHERE s.name = 'core' AND t.name = 'AuditLogs'
                )
                BEGIN
                    -- 1. 調整 TraceId 欄位長度
                    IF EXISTS (
                        SELECT 1 FROM sys.columns 
                        WHERE object_id = OBJECT_ID(N'[core].[AuditLogs]') AND name = 'TraceId'
                    )
                    BEGIN
                        ALTER TABLE [core].[AuditLogs] ALTER COLUMN [TraceId] VARCHAR(64) NOT NULL;
                    END

                    -- 2. 調整 Timestamp 預設值與型態
                    IF EXISTS (
                        SELECT 1 FROM sys.columns 
                        WHERE object_id = OBJECT_ID(N'[core].[AuditLogs]') AND name = 'Timestamp'
                    )
                    BEGIN
                        ALTER TABLE [core].[AuditLogs] ALTER COLUMN [Timestamp] DATETIMEOFFSET(7) NOT NULL;
                    END

                    -- 3. 調整 CreatedAt 預設值與型態
                    IF EXISTS (
                        SELECT 1 FROM sys.columns 
                        WHERE object_id = OBJECT_ID(N'[core].[AuditLogs]') AND name = 'CreatedAt'
                    )
                    BEGIN
                        ALTER TABLE [core].[AuditLogs] ALTER COLUMN [CreatedAt] DATETIMEOFFSET(7) NOT NULL;
                    END
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 防禦性復原邏輯
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 
                    FROM sys.tables t
                    JOIN sys.schemas s ON t.schema_id = s.schema_id
                    WHERE s.name = 'core' AND t.name = 'AuditLogs'
                )
                BEGIN
                    -- 1. 還原 TraceId 欄位長度
                    IF EXISTS (
                        SELECT 1 FROM sys.columns 
                        WHERE object_id = OBJECT_ID(N'[core].[AuditLogs]') AND name = 'TraceId'
                    )
                    BEGIN
                        ALTER TABLE [core].[AuditLogs] ALTER COLUMN [TraceId] CHAR(32) NOT NULL;
                    END

                    -- 2. 還原 Timestamp 欄位
                    IF EXISTS (
                        SELECT 1 FROM sys.columns 
                        WHERE object_id = OBJECT_ID(N'[core].[AuditLogs]') AND name = 'Timestamp'
                    )
                    BEGIN
                        ALTER TABLE [core].[AuditLogs] ALTER COLUMN [Timestamp] DATETIMEOFFSET(7) NOT NULL;
                    END

                    -- 3. 還原 CreatedAt 欄位
                    IF EXISTS (
                        SELECT 1 FROM sys.columns 
                        WHERE object_id = OBJECT_ID(N'[core].[AuditLogs]') AND name = 'CreatedAt'
                    )
                    BEGIN
                        ALTER TABLE [core].[AuditLogs] ALTER COLUMN [CreatedAt] DATETIMEOFFSET(7) NOT NULL;
                    END
                END
            ");
        }
    }
}

