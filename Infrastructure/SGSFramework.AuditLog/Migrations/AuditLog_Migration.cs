using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.EntityFrameworkCore.Migrations;
using SGSFramework.Core.Abstractions.Entities.Base;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuditLog.Migrations
{
    public partial class AuditLog_Migration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "org");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'org')
                BEGIN
                    EXEC('CREATE SCHEMA [org]');
                END;

                IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[org].[AuditLogs]') AND type IN (N'U'))
                BEGIN
                    CREATE TABLE [org].[AuditLogs] (
                        [Id] BIGINT IDENTITY(1,1) NOT NULL,
                        [TraceId] CHAR(32) NOT NULL,
                        [UserId] NVARCHAR(128) NULL,
                        [RemoteIp] NVARCHAR(64) NULL,
                        [CreatedAt] DATETIME2(7) NOT NULL CONSTRAINT [DF_AuditLogs_CreatedAt] DEFAULT (SYSUTCDATETIME()),
                        [Timestamp] DATETIME2(7) NOT NULL CONSTRAINT [DF_AuditLogs_Timestamp] DEFAULT (SYSUTCDATETIME()),
                        [Schema] NVARCHAR(64) NULL,
                        [TableName] NVARCHAR(128) NOT NULL,
                        [Action] NVARCHAR(50) NOT NULL,
                        [KeyValues] NVARCHAR(MAX) NULL,
                        [OldValues] NVARCHAR(MAX) NULL,
                        [NewValues] NVARCHAR(MAX) NULL,
                        [ChangedColumns] NVARCHAR(MAX) NULL,
                        [PreviousHash] NVARCHAR(128) NOT NULL,
                        [StoredHash] NVARCHAR(128) NOT NULL,
                        [IsRepaired] BIT NOT NULL CONSTRAINT [DF_AuditLogs_IsRepaired] DEFAULT (0),
                        [RepairedAt] DATETIME2(7) NULL,
                        [GapReason] NVARCHAR(500) NULL,
                        [OriginalStoredHash] NVARCHAR(128) NULL,
                        CONSTRAINT [PK_AuditLogs] PRIMARY KEY CLUSTERED ([Id] ASC, [CreatedAt] ASC)
                    )
                    WITH 
                    (
                        LEDGER = ON (APPEND_ONLY = ON)
                    );
                END;

                IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_AuditLog_TraceId_Covering' AND object_id = OBJECT_ID(N'[org].[AuditLogs]'))
                BEGIN
                    CREATE NONCLUSTERED INDEX [IX_AuditLog_TraceId_Covering]
                    ON [org].[AuditLogs] ([TraceId])
                    INCLUDE ([Action], [CreatedAt], [TableName]);
                END;

                IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_AuditLog_IsRepaired' AND object_id = OBJECT_ID(N'[org].[AuditLogs]'))
                BEGIN
                    CREATE NONCLUSTERED INDEX [IX_AuditLog_IsRepaired]
                    ON [org].[AuditLogs] ([IsRepaired])
                    INCLUDE ([TableName], [TraceId], [GapReason])
                    WHERE [IsRepaired] = 0;
                END;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS [org].[AuditLogs];");
        }
    }
}
