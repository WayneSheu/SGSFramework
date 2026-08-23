using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.SystemLog.Persistents
{
    public static class DbInitializer
    {
        /// <summary>
        /// Ensure that the SecurityLogs table exists in the database. This method is idempotent.
        /// </summary>
        /// <param name="connectionString"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public static void EnsureSecurityLogsTableExists(string connectionString)
        {
            // 1. 定義等冪性（Idempotent）的 DDL 腳本：若不存在則建立 Ledger 表
            const string ddlScript = @"
                               IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[core].[SecurityAuditLedger]') AND type in (N'U'))
                    BEGIN
                        -- 由於 Ledger 表具備防篡改鏈鎖定，必須透過專用程序或刪除總帳架構進行重置
                        DROP TABLE [core].[SecurityAuditLedger];
                    END

                    CREATE TABLE [core].[SecurityAuditLedger] (
                        [Id] INT IDENTITY(1,1) NOT NULL,
                        [Message] NVARCHAR(MAX) NULL,
                        [Level] NVARCHAR(128) NULL,
                        [Timestamp] DATETIMEOFFSET(7) NOT NULL,
                        [Exception] NVARCHAR(MAX) NULL,
                        [Properties] NVARCHAR(MAX) NULL,
                        -- 💡 安全審計專用強型別獨立欄位
                        [LogType] NVARCHAR(64) NULL,
                        [EventCategory] NVARCHAR(128) NULL,
                        [UserId] NVARCHAR(128) NULL,
                        [ClientIp] NVARCHAR(64) NULL,
                        CONSTRAINT [PK_SecurityAuditLedger] PRIMARY KEY CLUSTERED ([Id] ASC)
                    )
                    WITH (LEDGER = ON (APPEND_ONLY = ON)); -- 🔐 啟用總帳唯讀附加特徵
                    GO

                                    -- 建立最佳化索引
                                    CREATE NONCLUSTERED INDEX [IX_SecurityLogs_Timestamp] ON [core].[SecurityLogs] ([Timestamp] DESC);
                                    CREATE NONCLUSTERED INDEX [IX_SecurityLogs_UserId] ON [core].[SecurityLogs] ([UserId]);
                                    CREATE NONCLUSTERED INDEX [IX_SecurityLogs_CorrelationId] ON [core].[SecurityLogs] ([CorrelationId]);
                                END";

            try
            {
                // 2. 使用原生輕量連線執行，避免此時依賴任何日誌框架或 EF Core
                using var connection = new SqlConnection(connectionString);
                using var command = new SqlCommand(ddlScript, connection);

                connection.Open();
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                // 如果此處失敗，直接向外拋出異常，阻斷應用程式啟動，因為安全審計基礎建設未就緒
                throw new InvalidOperationException("無法初始化 SecurityLogs 安全性帳本資料表，系統拒絕啟動。", ex);
            }
        }
    }
}
