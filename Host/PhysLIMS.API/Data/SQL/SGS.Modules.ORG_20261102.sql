IF OBJECT_ID(N'[org].[__EFMigrationsHistory]') IS NULL
BEGIN
    IF SCHEMA_ID(N'org') IS NULL EXEC(N'CREATE SCHEMA [org];');
    CREATE TABLE [org].[__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF SCHEMA_ID(N'org') IS NULL EXEC(N'CREATE SCHEMA [org];');


                CREATE TABLE [org].[AuditLogs] (
                    [Id] bigint IDENTITY(1,1) NOT NULL,
                    [CreatedAt] DATETIMEOFFSET(7) NOT NULL CONSTRAINT [DF_AuditLogs_CreatedAt] DEFAULT (SYSDATETIMEOFFSET() AT TIME ZONE 'Taipei Standard Time'),
                    [TraceId] char(32) NOT NULL,
                    [UserId] nvarchar(128) NULL,
                    [RemoteIp] nvarchar(64) NULL,
                    [Timestamp] DATETIMEOFFSET(7) NOT NULL CONSTRAINT [DF_AuditLogs_Timestamp] DEFAULT (SYSDATETIMEOFFSET() AT TIME ZONE 'Taipei Standard Time'),
                    [Schema] nvarchar(64) NULL,
                    [TableName] nvarchar(128) NOT NULL,
                    [Action] nvarchar(50) NOT NULL,
                    [KeyValues] nvarchar(max) NULL,
                    [OldValues] nvarchar(max) NULL,
                    [NewValues] nvarchar(max) NULL,
                    [ChangedColumns] nvarchar(max) NULL,
                    [PreviousHash] nvarchar(128) NOT NULL,
                    [StoredHash] nvarchar(128) NOT NULL,
                    [IsRepaired] bit NOT NULL CONSTRAINT [DF_AuditLogs_IsRepaired] DEFAULT (0),
                    [RepairedAt] DATETIMEOFFSET(7) NULL,
                    [GapReason] nvarchar(500) NULL,
                    [OriginalStoredHash] nvarchar(128) NULL,
                    CONSTRAINT [PK_AuditLogs] PRIMARY KEY CLUSTERED ([Id] ASC)
                ) WITH (LEDGER = ON (APPEND_ONLY = ON));
            

CREATE TABLE [org].[Organization] (
    [Id] int NOT NULL IDENTITY,
    [ParentId] int NULL,
    [Code] nvarchar(20) NOT NULL,
    [Name] nvarchar(50) NOT NULL,
    [TenantLabId] uniqueidentifier NULL,
    [Location] nvarchar(max) NULL,
    [Description] nvarchar(200) NULL,
    [NodePath] nvarchar(max) NOT NULL,
    [Level] int NOT NULL DEFAULT 0,
    [CreatedBy] nvarchar(max) NOT NULL,
    [CreatedAtUtc] datetimeoffset NOT NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [UpdatedAtUtc] datetimeoffset NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedBy] nvarchar(max) NULL,
    [DeletedOnUtc] datetimeoffset NULL,
    CONSTRAINT [PK_Organization] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_Organization_TenantLabId_Level2Only] CHECK (([TenantLabId] IS NULL) OR ([Level] = 2)),
    CONSTRAINT [FK_Organization_Organization_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [org].[Organization] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [org].[OutboxMessages] (
    [Id] uniqueidentifier NOT NULL,
    [CorrelationId] varchar(100) NULL,
    [CausationId] varchar(200) NULL,
    [Type] varchar(500) NOT NULL,
    [Content] nvarchar(max) NOT NULL,
    [OccurredOnUtc] datetime2 NOT NULL,
    [ProcessedOnUtc] datetime2 NULL,
    [ScheduledAtUtc] datetime2 NULL,
    [RetryCount] int NOT NULL DEFAULT 0,
    [IsDead] bit NOT NULL DEFAULT CAST(0 AS bit),
    [LastError] nvarchar(max) NULL,
    CONSTRAINT [PK_OutboxMessages] PRIMARY KEY ([Id])
);

CREATE TABLE [org].[UserLaboratory] (
    [UserId] nvarchar(450) NOT NULL,
    [OrganizationId] int NOT NULL,
    [CreatedBy] nvarchar(max) NOT NULL,
    [CreatedAtUtc] datetimeoffset NOT NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [UpdatedAtUtc] datetimeoffset NULL,
    CONSTRAINT [PK_UserLaboratory] PRIMARY KEY ([UserId], [OrganizationId]),
    CONSTRAINT [FK_UserLaboratory_Organization_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [org].[Organization] ([Id]) ON DELETE CASCADE
);

CREATE UNIQUE INDEX [IX_Organization_Code] ON [org].[Organization] ([Code]);

CREATE INDEX [IX_Organization_IsDeleted] ON [org].[Organization] ([IsDeleted]);

CREATE INDEX [IX_Organization_ParentId] ON [org].[Organization] ([ParentId]);

CREATE INDEX [IX_Organization_TenantLabId] ON [org].[Organization] ([TenantLabId]) WHERE [TenantLabId] IS NOT NULL;

CREATE INDEX [IX_org_OutboxMessages_CorrelationId] ON [org].[OutboxMessages] ([CorrelationId]);

CREATE INDEX [IX_org_OutboxMessages_FetchPending] ON [org].[OutboxMessages] ([ProcessedOnUtc], [IsDead], [ScheduledAtUtc]);

CREATE INDEX [IX_UserLaboratory_OrganizationId] ON [org].[UserLaboratory] ([OrganizationId]);

INSERT INTO [org].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260823060423_ORGDB_Initial', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [org].[Organization] ADD [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit);

INSERT INTO [org].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260827033501_Update_organization_isactive', N'10.0.12');

COMMIT;
GO



BEGIN TRANSACTION;
GO
   


INSERT INTO [org].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260828052423_CreateUserAccessibleLabsView', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
DROP TABLE [org].[UserLaboratory];

INSERT INTO [org].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260829111109_delete_user_labary', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
DECLARE @description AS sql_variant;
SET @description = N'Outbox 訊息實體，用於實作「發佈者確認」模式 (Publisher Confirmed)';
EXEC sp_addextendedproperty 'MS_Description', @description, 'SCHEMA', N'org', 'TABLE', N'OutboxMessages';

DECLARE @description1 AS sql_variant;
SET @description1 = N'組織/實驗室實體 (Hierarchical Entity)';
EXEC sp_addextendedproperty 'MS_Description', @description1, 'SCHEMA', N'org', 'TABLE', N'Organization';

DECLARE @description2 AS sql_variant;
SET @description2 = N'資料稽核日誌實體';
EXEC sp_addextendedproperty 'MS_Description', @description2, 'SCHEMA', N'org', 'TABLE', N'AuditLogs';

DECLARE @description3 AS sql_variant;
SET @description3 = N'事件的完整型別名稱 (包含 Namespace)，用於還原物件';
EXEC sp_addextendedproperty 'MS_Description', @description3, 'SCHEMA', N'org', 'TABLE', N'OutboxMessages', 'COLUMN', N'Type';

DECLARE @description4 AS sql_variant;
SET @description4 = N'預計下次重試的時間 (UTC)。用於實作「指數退避」，避免失敗後立即重試造成系統壓力。';
EXEC sp_addextendedproperty 'MS_Description', @description4, 'SCHEMA', N'org', 'TABLE', N'OutboxMessages', 'COLUMN', N'ScheduledAtUtc';

DECLARE @description5 AS sql_variant;
SET @description5 = N'目前已重試的次數';
EXEC sp_addextendedproperty 'MS_Description', @description5, 'SCHEMA', N'org', 'TABLE', N'OutboxMessages', 'COLUMN', N'RetryCount';

DECLARE @description6 AS sql_variant;
SET @description6 = N'訊息被成功處理的時間。若為 NULL 且 IsDead 為 false，表示待處理；若為 1900-01-01，表示正被某個 Worker 鎖定處理中。';
EXEC sp_addextendedproperty 'MS_Description', @description6, 'SCHEMA', N'org', 'TABLE', N'OutboxMessages', 'COLUMN', N'ProcessedOnUtc';

DECLARE @description7 AS sql_variant;
SET @description7 = N'事件發生的原始時間 (UTC)';
EXEC sp_addextendedproperty 'MS_Description', @description7, 'SCHEMA', N'org', 'TABLE', N'OutboxMessages', 'COLUMN', N'OccurredOnUtc';

DECLARE @description8 AS sql_variant;
SET @description8 = N'最後一次處理失敗的錯誤訊息內容，便於維運排錯';
EXEC sp_addextendedproperty 'MS_Description', @description8, 'SCHEMA', N'org', 'TABLE', N'OutboxMessages', 'COLUMN', N'LastError';

DECLARE @description9 AS sql_variant;
SET @description9 = N'當 RetryCount 超過閾值後標記為 true，背景服務將不再主動抓取。';
EXEC sp_addextendedproperty 'MS_Description', @description9, 'SCHEMA', N'org', 'TABLE', N'OutboxMessages', 'COLUMN', N'IsDead';

DECLARE @description10 AS sql_variant;
SET @description10 = N'關聯 ID：代表整個業務流程的追蹤編號 (由前端或 API 傳入)';
EXEC sp_addextendedproperty 'MS_Description', @description10, 'SCHEMA', N'org', 'TABLE', N'OutboxMessages', 'COLUMN', N'CorrelationId';

DECLARE @description11 AS sql_variant;
SET @description11 = N'事件序列化後的 JSON 內容';
EXEC sp_addextendedproperty 'MS_Description', @description11, 'SCHEMA', N'org', 'TABLE', N'OutboxMessages', 'COLUMN', N'Content';

DECLARE @description12 AS sql_variant;
SET @description12 = N'誘發 ID：代表觸發此事件的具體 Command 名稱或 ID';
EXEC sp_addextendedproperty 'MS_Description', @description12, 'SCHEMA', N'org', 'TABLE', N'OutboxMessages', 'COLUMN', N'CausationId';

DECLARE @description13 AS sql_variant;
SET @description13 = N'識別主鍵';
EXEC sp_addextendedproperty 'MS_Description', @description13, 'SCHEMA', N'org', 'TABLE', N'OutboxMessages', 'COLUMN', N'Id';

DECLARE @description14 AS sql_variant;
SET @description14 = N'更新者';
EXEC sp_addextendedproperty 'MS_Description', @description14, 'SCHEMA', N'org', 'TABLE', N'Organization', 'COLUMN', N'UpdatedBy';

DECLARE @description15 AS sql_variant;
SET @description15 = N'更新時間';
EXEC sp_addextendedproperty 'MS_Description', @description15, 'SCHEMA', N'org', 'TABLE', N'Organization', 'COLUMN', N'UpdatedAtUtc';

DECLARE @description16 AS sql_variant;
SET @description16 = N'僅 Level 2 區域實驗室實體可寫入 DB 的 TenantLabId';
EXEC sp_addextendedproperty 'MS_Description', @description16, 'SCHEMA', N'org', 'TABLE', N'Organization', 'COLUMN', N'TenantLabId';

DECLARE @description17 AS sql_variant;
SET @description17 = N'父節點 ID';
EXEC sp_addextendedproperty 'MS_Description', @description17, 'SCHEMA', N'org', 'TABLE', N'Organization', 'COLUMN', N'ParentId';

DECLARE @description18 AS sql_variant;
SET @description18 = N'節點路徑';
EXEC sp_addextendedproperty 'MS_Description', @description18, 'SCHEMA', N'org', 'TABLE', N'Organization', 'COLUMN', N'NodePath';

DECLARE @description19 AS sql_variant;
SET @description19 = N'組織/實驗室名稱';
EXEC sp_addextendedproperty 'MS_Description', @description19, 'SCHEMA', N'org', 'TABLE', N'Organization', 'COLUMN', N'Name';

DECLARE @description20 AS sql_variant;
SET @description20 = N'組織/實驗室位置';
EXEC sp_addextendedproperty 'MS_Description', @description20, 'SCHEMA', N'org', 'TABLE', N'Organization', 'COLUMN', N'Location';

DECLARE @description21 AS sql_variant;
SET @description21 = N'層級 (Level)';
EXEC sp_addextendedproperty 'MS_Description', @description21, 'SCHEMA', N'org', 'TABLE', N'Organization', 'COLUMN', N'Level';

DECLARE @description22 AS sql_variant;
SET @description22 = N'是否已刪除';
EXEC sp_addextendedproperty 'MS_Description', @description22, 'SCHEMA', N'org', 'TABLE', N'Organization', 'COLUMN', N'IsDeleted';

DECLARE @description23 AS sql_variant;
SET @description23 = N'是否啟用';
EXEC sp_addextendedproperty 'MS_Description', @description23, 'SCHEMA', N'org', 'TABLE', N'Organization', 'COLUMN', N'IsActive';

DECLARE @description24 AS sql_variant;
SET @description24 = N'組織/實驗室描述';
EXEC sp_addextendedproperty 'MS_Description', @description24, 'SCHEMA', N'org', 'TABLE', N'Organization', 'COLUMN', N'Description';

DECLARE @description25 AS sql_variant;
SET @description25 = N'刪除時間';
EXEC sp_addextendedproperty 'MS_Description', @description25, 'SCHEMA', N'org', 'TABLE', N'Organization', 'COLUMN', N'DeletedOnUtc';

DECLARE @description26 AS sql_variant;
SET @description26 = N'刪除者';
EXEC sp_addextendedproperty 'MS_Description', @description26, 'SCHEMA', N'org', 'TABLE', N'Organization', 'COLUMN', N'DeletedBy';

DECLARE @description27 AS sql_variant;
SET @description27 = N'建立者';
EXEC sp_addextendedproperty 'MS_Description', @description27, 'SCHEMA', N'org', 'TABLE', N'Organization', 'COLUMN', N'CreatedBy';

DECLARE @description28 AS sql_variant;
SET @description28 = N'建立時間';
EXEC sp_addextendedproperty 'MS_Description', @description28, 'SCHEMA', N'org', 'TABLE', N'Organization', 'COLUMN', N'CreatedAtUtc';

DECLARE @description29 AS sql_variant;
SET @description29 = N'組織/實驗室代碼';
EXEC sp_addextendedproperty 'MS_Description', @description29, 'SCHEMA', N'org', 'TABLE', N'Organization', 'COLUMN', N'Code';

DECLARE @description30 AS sql_variant;
SET @description30 = N'組織/實驗室 ID';
EXEC sp_addextendedproperty 'MS_Description', @description30, 'SCHEMA', N'org', 'TABLE', N'Organization', 'COLUMN', N'Id';

DECLARE @description31 AS sql_variant;
SET @description31 = N'使用者識別';
EXEC sp_addextendedproperty 'MS_Description', @description31, 'SCHEMA', N'org', 'TABLE', N'AuditLogs', 'COLUMN', N'UserId';

DECLARE @description32 AS sql_variant;
SET @description32 = N'全鏈路追蹤 ID (對應 W3C TraceId 格式 32 位元 HEX)';
EXEC sp_addextendedproperty 'MS_Description', @description32, 'SCHEMA', N'org', 'TABLE', N'AuditLogs', 'COLUMN', N'TraceId';

DECLARE @description33 AS sql_variant;
SET @description33 = N'時間戳記';
EXEC sp_addextendedproperty 'MS_Description', @description33, 'SCHEMA', N'org', 'TABLE', N'AuditLogs', 'COLUMN', N'Timestamp';

DECLARE @description34 AS sql_variant;
SET @description34 = N'資料表名稱';
EXEC sp_addextendedproperty 'MS_Description', @description34, 'SCHEMA', N'org', 'TABLE', N'AuditLogs', 'COLUMN', N'TableName';

DECLARE @description35 AS sql_variant;
SET @description35 = N'儲存雜湊值';
EXEC sp_addextendedproperty 'MS_Description', @description35, 'SCHEMA', N'org', 'TABLE', N'AuditLogs', 'COLUMN', N'StoredHash';

DECLARE @description36 AS sql_variant;
SET @description36 = N'資料表綱要';
EXEC sp_addextendedproperty 'MS_Description', @description36, 'SCHEMA', N'org', 'TABLE', N'AuditLogs', 'COLUMN', N'Schema';

DECLARE @description37 AS sql_variant;
SET @description37 = N'修復時間';
EXEC sp_addextendedproperty 'MS_Description', @description37, 'SCHEMA', N'org', 'TABLE', N'AuditLogs', 'COLUMN', N'RepairedAt';

DECLARE @description38 AS sql_variant;
SET @description38 = N'遠端 IP 位址';
EXEC sp_addextendedproperty 'MS_Description', @description38, 'SCHEMA', N'org', 'TABLE', N'AuditLogs', 'COLUMN', N'RemoteIp';

DECLARE @description39 AS sql_variant;
SET @description39 = N'前一筆雜湊值';
EXEC sp_addextendedproperty 'MS_Description', @description39, 'SCHEMA', N'org', 'TABLE', N'AuditLogs', 'COLUMN', N'PreviousHash';

DECLARE @description40 AS sql_variant;
SET @description40 = N'原始儲存雜湊值';
EXEC sp_addextendedproperty 'MS_Description', @description40, 'SCHEMA', N'org', 'TABLE', N'AuditLogs', 'COLUMN', N'OriginalStoredHash';

DECLARE @description41 AS sql_variant;
SET @description41 = N'舊值';
EXEC sp_addextendedproperty 'MS_Description', @description41, 'SCHEMA', N'org', 'TABLE', N'AuditLogs', 'COLUMN', N'OldValues';

DECLARE @description42 AS sql_variant;
SET @description42 = N'新值';
EXEC sp_addextendedproperty 'MS_Description', @description42, 'SCHEMA', N'org', 'TABLE', N'AuditLogs', 'COLUMN', N'NewValues';

DECLARE @description43 AS sql_variant;
SET @description43 = N'主鍵值';
EXEC sp_addextendedproperty 'MS_Description', @description43, 'SCHEMA', N'org', 'TABLE', N'AuditLogs', 'COLUMN', N'KeyValues';

DECLARE @description44 AS sql_variant;
SET @description44 = N'是否已修復';
EXEC sp_addextendedproperty 'MS_Description', @description44, 'SCHEMA', N'org', 'TABLE', N'AuditLogs', 'COLUMN', N'IsRepaired';

DECLARE @description45 AS sql_variant;
SET @description45 = N'差異原因';
EXEC sp_addextendedproperty 'MS_Description', @description45, 'SCHEMA', N'org', 'TABLE', N'AuditLogs', 'COLUMN', N'GapReason';

DECLARE @description46 AS sql_variant;
SET @description46 = N'建立時間';
EXEC sp_addextendedproperty 'MS_Description', @description46, 'SCHEMA', N'org', 'TABLE', N'AuditLogs', 'COLUMN', N'CreatedAt';

DECLARE @description47 AS sql_variant;
SET @description47 = N'變更欄位';
EXEC sp_addextendedproperty 'MS_Description', @description47, 'SCHEMA', N'org', 'TABLE', N'AuditLogs', 'COLUMN', N'ChangedColumns';

DECLARE @description48 AS sql_variant;
SET @description48 = N'操作類型';
EXEC sp_addextendedproperty 'MS_Description', @description48, 'SCHEMA', N'org', 'TABLE', N'AuditLogs', 'COLUMN', N'Action';

DECLARE @description49 AS sql_variant;
SET @description49 = N'稽核日誌識別主鍵';
EXEC sp_addextendedproperty 'MS_Description', @description49, 'SCHEMA', N'org', 'TABLE', N'AuditLogs', 'COLUMN', N'Id';

INSERT INTO [org].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261025133203_AddOrgOutboxComments', N'10.0.12');

COMMIT;
GO

 CREATE OR ALTER VIEW [org].[vw_UserAccessibleLabs] AS
    WITH Level1Orgs AS (
        SELECT 
            Id,
            TenantLabId,
            Code,
            Name,
            NodePath
        FROM [org].[Organization] WITH (NOLOCK)
        WHERE Level = 1 AND IsActive = 1 AND IsDeleted = 0
    )
    SELECT 
        ul.UserId,
        lab.Id AS LabId,
        lab.TenantLabId AS TenantLabId,
        lab.Code AS LabCode,
        lab.Name AS LabName,
        lab.NodePath AS Path,
        lab.Level AS HierarchyLevel,
        ul.IsPrimary,
        ISNULL(p.Id, lab.Id) AS ParentLabId,
        ISNULL(p.TenantLabId, lab.TenantLabId) AS ParentTenantLabId,
        ISNULL(p.Code, lab.Code) AS ParentLabCode,
        ISNULL(p.Name, lab.Name) AS ParentLabName
    FROM [core].[UserLabMappings] ul WITH (NOLOCK)
    INNER JOIN [org].[Organization] lab WITH (NOLOCK) 
        ON ul.TenantLabId = lab.TenantLabId
    LEFT JOIN Level1Orgs p 
        ON CAST(lab.NodePath AS hierarchyid).IsDescendantOf(CAST(p.NodePath AS hierarchyid)) = 1
    WHERE ul.IsActive = 1 AND lab.IsActive = 1 AND lab.IsDeleted = 0;