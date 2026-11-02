IF OBJECT_ID(N'[core].[__EFMigrationsHistory]') IS NULL
BEGIN
    IF SCHEMA_ID(N'core') IS NULL EXEC(N'CREATE SCHEMA [core];');
    CREATE TABLE [core].[__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF SCHEMA_ID(N'core') IS NULL EXEC(N'CREATE SCHEMA [core];');

CREATE TABLE [core].[AspNetRoles] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(256) NULL,
    [NormalizedName] nvarchar(256) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
);

CREATE TABLE [core].[AspNetUsers] (
    [Id] uniqueidentifier NOT NULL,
    [FullName] nvarchar(max) NOT NULL,
    [LastLoginAt] datetimeoffset NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [UserName] nvarchar(256) NULL,
    [NormalizedUserName] nvarchar(256) NULL,
    [Email] nvarchar(256) NULL,
    [NormalizedEmail] nvarchar(256) NULL,
    [EmailConfirmed] bit NOT NULL,
    [PasswordHash] nvarchar(max) NULL,
    [SecurityStamp] nvarchar(max) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    [PhoneNumber] nvarchar(max) NULL,
    [PhoneNumberConfirmed] bit NOT NULL,
    [TwoFactorEnabled] bit NOT NULL,
    [LockoutEnd] datetimeoffset NULL,
    [LockoutEnabled] bit NOT NULL,
    [AccessFailedCount] int NOT NULL,
    CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
);


                CREATE TABLE [core].[AuditLogs] (
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
            

CREATE TABLE [core].[ControllerMetadatas] (
    [Id] uniqueidentifier NOT NULL,
    [Version] nvarchar(max) NOT NULL,
    [ModuleName] nvarchar(50) NOT NULL,
    [ModuleTitle] nvarchar(100) NOT NULL,
    [ControllerTitle] nvarchar(100) NOT NULL,
    [ControllerName] nvarchar(100) NOT NULL,
    [ControllerTypeName] nvarchar(max) NOT NULL,
    [ActionName] nvarchar(450) NOT NULL,
    [RouteTemplate] nvarchar(250) NOT NULL,
    [Description] nvarchar(max) NULL,
    [DisplayName] nvarchar(100) NOT NULL,
    [Icon] nvarchar(max) NULL,
    [DisplayOrder] int NOT NULL,
    [ParentMenuName] nvarchar(max) NULL,
    [PermissionKey] nvarchar(50) NOT NULL,
    [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
    [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    CONSTRAINT [PK_ControllerMetadatas] PRIMARY KEY ([Id])
);

CREATE TABLE [core].[MenuItems] (
    [Id] uniqueidentifier NOT NULL,
    [ControllerId] uniqueidentifier NULL,
    [ParentId] uniqueidentifier NULL,
    [DisplayName] nvarchar(100) NOT NULL,
    [Icon] nvarchar(max) NULL,
    [Route] nvarchar(200) NULL,
    [DisplayOrder] int NOT NULL,
    [PermissionKey] nvarchar(50) NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_MenuItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MenuItems_MenuItems_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [core].[MenuItems] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [core].[ModuleMetadatas] (
    [Id] uniqueidentifier NOT NULL,
    [ModuleTitle] nvarchar(100) NOT NULL,
    [ModuleName] nvarchar(100) NOT NULL,
    [Version] nvarchar(20) NOT NULL DEFAULT N'1.0.0',
    [AssemblyPath] nvarchar(500) NOT NULL,
    [IsActive] bit NOT NULL,
    [LastLoadedAt] datetime2 NOT NULL,
    [Checksum] nvarchar(64) NOT NULL,
    CONSTRAINT [PK_ModuleMetadatas] PRIMARY KEY ([Id])
);

CREATE TABLE [core].[OutboxMessages] (
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

CREATE TABLE [core].[PermissionGrants] (
    [Id] uniqueidentifier NOT NULL,
    [RoleId] uniqueidentifier NOT NULL,
    [LabId] uniqueidentifier NOT NULL,
    [PermissionVector] varbinary(64) NOT NULL,
    CONSTRAINT [PK_PermissionGrants] PRIMARY KEY ([Id])
);

CREATE TABLE [core].[Permissions] (
    [Id] int NOT NULL IDENTITY,
    [PermissionKey] nvarchar(128) NOT NULL,
    [BitPosition] int NOT NULL,
    [ModuleName] nvarchar(max) NOT NULL,
    [ControllerName] nvarchar(max) NOT NULL,
    [ActionName] nvarchar(max) NOT NULL,
    [Description] nvarchar(256) NOT NULL,
    CONSTRAINT [PK_Permissions] PRIMARY KEY ([Id])
);

CREATE TABLE [core].[RemediationTickets] (
    [TicketId] nvarchar(128) NOT NULL,
    [UserId] nvarchar(128) NOT NULL,
    [IsUsed] bit NOT NULL,
    [IssuedAt] datetime2 NOT NULL,
    [ExpiresAt] datetime2 NOT NULL,
    [RemediationReason] nvarchar(500) NULL,
    [CreatedByIp] nvarchar(64) NOT NULL,
    CONSTRAINT [PK_RemediationTickets] PRIMARY KEY ([TicketId])
);


                CREATE TABLE [core].[SecurityLog] (
                    [Id] int IDENTITY(1,1) NOT NULL,
                    [CorrelationId] varchar(50) NULL,
                    [Message] NVARCHAR(MAX) NULL,
                    [Level] NVARCHAR(128) NULL,
                    [Timestamp] DATETIMEOFFSET(7) NOT NULL CONSTRAINT [DF_SecurityLog_Timestamp] DEFAULT (SYSDATETIMEOFFSET() AT TIME ZONE 'Taipei Standard Time'),
                    [Exception] NVARCHAR(MAX) NULL,
                    [Properties] NVARCHAR(MAX) NULL,
                    [LogType] NVARCHAR(64) NULL,
                    [EventCategory] NVARCHAR(128) NULL,
                    [UserId] NVARCHAR(128) NULL,
                    [ClientIp] NVARCHAR(64) NULL,
                    [AlertId] char(32) NULL,
                    [Fingerprint] char(64) NULL,
                    CONSTRAINT [PK_SecurityLog] PRIMARY KEY CLUSTERED ([Id] ASC)
                ) WITH (LEDGER = ON (APPEND_ONLY = ON));
            


            CREATE TABLE [core].[SystemLogs] (
            [Id] bigint IDENTITY(1,1) NOT NULL,
            [TimeStamp] DATETIMEOFFSET(7) NOT NULL CONSTRAINT [DF_SystemLogs_TimeStamp] DEFAULT (SYSDATETIMEOFFSET() AT TIME ZONE 'Taipei Standard Time'),
            [Message] nvarchar(max) NULL,
            [Level] nvarchar(128) NULL,
            [Exception] nvarchar(max) NULL,
            [TenantId] nvarchar(50) NULL,
            [UserId] nvarchar(50) NULL,
            [ModuleName] nvarchar(50) NULL,
            [Operation] nvarchar(max) NULL,
            [CorrelationId] varchar(50) NULL,
            [IP] varchar(45) NULL,
            [Url] nvarchar(2083) NULL,
            [Payload] nvarchar(max) NULL,
            [PrevHash] nvarchar(64) NULL,
            [CurrentHash] nvarchar(64) NULL,
            [CreatedAt] DATETIMEOFFSET(7) NOT NULL,
            [AlertId] char(32) NULL,
            [Fingerprint] char(64) NULL,
            CONSTRAINT [PK_SystemLogs] PRIMARY KEY CLUSTERED ([Id] ASC)
             ) WITH (LEDGER = ON (APPEND_ONLY = ON));
            

CREATE TABLE [core].[UserRefreshTokens] (
    [Id] bigint NOT NULL IDENTITY,
    [UserId] nvarchar(100) NOT NULL,
    [DeviceId] nvarchar(255) NOT NULL,
    [DeviceName] nvarchar(200) NOT NULL,
    [RefreshTokenHash] nchar(64) NOT NULL,
    [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    [RotatedAt] datetime2 NULL,
    [ReusedTokenCache] nvarchar(64) NULL,
    [ExpiresAt] datetime2 NOT NULL,
    [LastActiveAt] datetime2 NOT NULL,
    [ClientIp] nvarchar(50) NOT NULL,
    [IsDead] bit NOT NULL DEFAULT CAST(0 AS bit),
    [IsFrozen] bit NOT NULL,
    [RiskReason] nvarchar(max) NULL,
    CONSTRAINT [PK_UserRefreshTokens] PRIMARY KEY ([Id])
);

CREATE TABLE [core].[AspNetRoleClaims] (
    [Id] int NOT NULL IDENTITY,
    [RoleId] uniqueidentifier NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [core].[AspNetRoles] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [core].[AspNetUserClaims] (
    [Id] int NOT NULL IDENTITY,
    [UserId] uniqueidentifier NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [core].[AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [core].[AspNetUserLogins] (
    [LoginProvider] nvarchar(450) NOT NULL,
    [ProviderKey] nvarchar(450) NOT NULL,
    [ProviderDisplayName] nvarchar(max) NULL,
    [UserId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
    CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [core].[AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [core].[AspNetUserRoles] (
    [UserId] uniqueidentifier NOT NULL,
    [RoleId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [core].[AspNetRoles] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [core].[AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [core].[AspNetUserTokens] (
    [UserId] uniqueidentifier NOT NULL,
    [LoginProvider] nvarchar(450) NOT NULL,
    [Name] nvarchar(450) NOT NULL,
    [Value] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
    CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [core].[AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [core].[UserLabMappings] (
    [UserId] uniqueidentifier NOT NULL,
    [LabId] int NOT NULL,
    [TenantLabId] uniqueidentifier NOT NULL,
    [IsPrimary] bit NOT NULL DEFAULT CAST(0 AS bit),
    [JobTitle] nvarchar(50) NULL,
    [EffectiveDate] datetime2(7) NOT NULL,
    [ExpiryDate] datetime2(7) NULL,
    [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
    [CreatedAtUtc] datetimeoffset(7) NOT NULL DEFAULT (SYSDATETIMEOFFSET()),
    [CreatedBy] nvarchar(100) NULL,
    [UpdatedAtUtc] datetimeoffset(7) NULL,
    [UpdatedBy] nvarchar(100) NULL,
    CONSTRAINT [PK_UserLabMappings] PRIMARY KEY ([UserId], [LabId]),
    CONSTRAINT [FK_UserLabMappings_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [core].[AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [core].[UserResourceGrants] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [MenuItemId] uniqueidentifier NOT NULL,
    [LabId] uniqueidentifier NOT NULL,
    [PermissionType] nvarchar(20) NOT NULL,
    [EffectType] nvarchar(10) NOT NULL,
    [DelegatorUserId] uniqueidentifier NULL,
    [ValidFrom] datetimeoffset NULL,
    [ValidTo] datetimeoffset NULL,
    CONSTRAINT [PK_UserResourceGrants] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_UserResourceGrants_MenuItems_MenuItemId] FOREIGN KEY ([MenuItemId]) REFERENCES [core].[MenuItems] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [core].[AspNetRoleClaims] ([RoleId]);

CREATE UNIQUE INDEX [RoleNameIndex] ON [core].[AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;

CREATE INDEX [IX_AspNetUserClaims_UserId] ON [core].[AspNetUserClaims] ([UserId]);

CREATE INDEX [IX_AspNetUserLogins_UserId] ON [core].[AspNetUserLogins] ([UserId]);

CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [core].[AspNetUserRoles] ([RoleId]);

CREATE INDEX [EmailIndex] ON [core].[AspNetUsers] ([NormalizedEmail]);

CREATE UNIQUE INDEX [UserNameIndex] ON [core].[AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;

CREATE INDEX [IX_AuditLog_IsRepaired] ON [core].[AuditLogs] ([IsRepaired]) INCLUDE ([TableName], [TraceId], [GapReason]) WHERE [IsRepaired] = 0;

CREATE INDEX [IX_AuditLog_TraceId_Covering] ON [core].[AuditLogs] ([TraceId]) INCLUDE ([Action], [CreatedAt], [TableName]);

CREATE INDEX [IX_Metadata_ModuleName] ON [core].[ControllerMetadatas] ([ModuleName]);

CREATE INDEX [IX_Metadata_PermissionKey] ON [core].[ControllerMetadatas] ([PermissionKey]);

CREATE UNIQUE INDEX [UX_Module_Controller_Action] ON [core].[ControllerMetadatas] ([ModuleName], [ControllerName], [ActionName]);

CREATE INDEX [IX_MenuItems_DisplayOrder] ON [core].[MenuItems] ([DisplayOrder]);

CREATE INDEX [IX_MenuItems_ParentId] ON [core].[MenuItems] ([ParentId]);

CREATE UNIQUE INDEX [IX_ModuleMetadatas_ModuleName] ON [core].[ModuleMetadatas] ([ModuleName]);

CREATE INDEX [IX_core_OutboxMessages_CorrelationId] ON [core].[OutboxMessages] ([CorrelationId]);

CREATE INDEX [IX_core_OutboxMessages_FetchPending] ON [core].[OutboxMessages] ([ProcessedOnUtc], [IsDead], [ScheduledAtUtc]);

CREATE UNIQUE INDEX [IX_PermissionGrant_Role_Lab] ON [core].[PermissionGrants] ([RoleId], [LabId]);

CREATE UNIQUE INDEX [IX_Permissions_BitPosition] ON [core].[Permissions] ([BitPosition]);

CREATE UNIQUE INDEX [IX_Permissions_PermissionKey] ON [core].[Permissions] ([PermissionKey]);

CREATE INDEX [IX_RemediationTickets_TicketId_UserId] ON [core].[RemediationTickets] ([TicketId], [UserId]);

CREATE INDEX [IX_SecurityLog_CorrelationId] ON [core].[SecurityLog] ([CorrelationId]);

CREATE INDEX [IX_SystemLogs_CorrelationId] ON [core].[SystemLogs] ([CorrelationId]);

CREATE INDEX [IX_SystemLogs_TenantId] ON [core].[SystemLogs] ([TenantId]);

CREATE INDEX [IX_SystemLogs_TimeStamp] ON [core].[SystemLogs] ([TimeStamp]);

CREATE INDEX [IX_UserLabMappings_EffectiveRange] ON [core].[UserLabMappings] ([UserId], [IsActive], [EffectiveDate], [ExpiryDate]);

CREATE INDEX [IX_UserLabMappings_TenantLabId_UserId] ON [core].[UserLabMappings] ([TenantLabId], [UserId], [IsActive]);

CREATE UNIQUE INDEX [UX_UserLabMappings_OnePrimaryPerUser] ON [core].[UserLabMappings] ([UserId]) WHERE [IsPrimary] = 1 AND [IsActive] = 1;

CREATE INDEX [IX_UserRefreshTokens_RefreshTokenHash] ON [core].[UserRefreshTokens] ([RefreshTokenHash]);

CREATE INDEX [IX_UserRefreshTokens_UserId_DeviceId_IsDead] ON [core].[UserRefreshTokens] ([UserId], [DeviceId], [IsDead]);

CREATE UNIQUE INDEX [UX_UserRefreshTokens_UserId_DeviceId] ON [core].[UserRefreshTokens] ([UserId], [DeviceId]);

CREATE INDEX [IX_UserResource_Lookup] ON [core].[UserResourceGrants] ([UserId], [MenuItemId], [LabId]);

CREATE INDEX [IX_UserResourceGrants_MenuItemId] ON [core].[UserResourceGrants] ([MenuItemId]);

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260824065358_PhysLIMSDb_Initial', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [core].[ControllerMetadatas] ADD [AttributesJson] nvarchar(max) NULL;

ALTER TABLE [core].[ControllerMetadatas] ADD [BitPosition] int NULL;

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260901014857_update_controller_metadata', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [core].[User_Global_Permissions] (
    [id] uniqueidentifier NOT NULL,
    [user_id] nvarchar(450) NOT NULL,
    [permission_key] nvarchar(150) NOT NULL,
    [bitmask] bigint NOT NULL,
    [created_at] datetime2 NOT NULL,
    [updated_at] datetime2 NULL,
    CONSTRAINT [PK_User_Global_Permissions] PRIMARY KEY ([id])
);

CREATE TABLE [core].[User_Lab_Permissions] (
    [id] uniqueidentifier NOT NULL,
    [user_id] nvarchar(450) NOT NULL,
    [tenant_lab_id] uniqueidentifier NOT NULL,
    [controller_or_module_key] nvarchar(150) NOT NULL,
    [bitmask] bigint NOT NULL,
    [created_at] datetime2 NOT NULL,
    [updated_at] datetime2 NULL,
    CONSTRAINT [PK_User_Lab_Permissions] PRIMARY KEY ([id])
);

CREATE UNIQUE INDEX [ix_user_global_permissions_user_key] ON [core].[User_Global_Permissions] ([user_id], [permission_key]);

CREATE UNIQUE INDEX [ix_user_lab_permissions_user_lab_controller] ON [core].[User_Lab_Permissions] ([user_id], [tenant_lab_id], [controller_or_module_key]);

CREATE INDEX [ix_user_lab_permissions_user_tenant] ON [core].[User_Lab_Permissions] ([user_id], [tenant_lab_id]);

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260901140320_user_lab_permission', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [core].[AspNetRoles] ADD [Code] nvarchar(max) NOT NULL DEFAULT N'';

ALTER TABLE [core].[AspNetRoles] ADD [Description] nvarchar(max) NOT NULL DEFAULT N'';

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260901145440_update_role', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
DECLARE @var nvarchar(max);
SELECT @var = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[AspNetRoles]') AND [c].[name] = N'Code');
IF @var IS NOT NULL EXEC(N'ALTER TABLE [core].[AspNetRoles] DROP CONSTRAINT ' + @var + ';');
ALTER TABLE [core].[AspNetRoles] DROP COLUMN [Code];

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260902011504_Update_Role_Description', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
DROP TABLE [core].[Permissions];

CREATE TABLE [core].[PermissionMetadata] (
    [Id] int NOT NULL IDENTITY,
    [PermissionKey] nvarchar(128) NOT NULL,
    [BitPosition] int NOT NULL,
    [ModuleName] nvarchar(max) NOT NULL,
    [ControllerName] nvarchar(max) NOT NULL,
    [ActionName] nvarchar(max) NOT NULL,
    [Description] nvarchar(256) NOT NULL,
    CONSTRAINT [PK_PermissionMetadata] PRIMARY KEY ([Id])
);

CREATE UNIQUE INDEX [IX_PermissionMetadata_BitPosition] ON [core].[PermissionMetadata] ([BitPosition]);

CREATE UNIQUE INDEX [IX_PermissionMetadata_PermissionKey] ON [core].[PermissionMetadata] ([PermissionKey]);

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260902024423_change_permission_name', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
DECLARE @var1 nvarchar(max);
SELECT @var1 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[PermissionMetadata]') AND [c].[name] = N'ModuleName');
IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [core].[PermissionMetadata] DROP CONSTRAINT ' + @var1 + ';');
ALTER TABLE [core].[PermissionMetadata] ALTER COLUMN [ModuleName] nvarchar(128) NOT NULL;

DECLARE @var2 nvarchar(max);
SELECT @var2 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[PermissionMetadata]') AND [c].[name] = N'ControllerName');
IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [core].[PermissionMetadata] DROP CONSTRAINT ' + @var2 + ';');
ALTER TABLE [core].[PermissionMetadata] ALTER COLUMN [ControllerName] nvarchar(128) NOT NULL;

ALTER TABLE [core].[PermissionMetadata] ADD [ControllerTitle] nvarchar(128) NULL;

ALTER TABLE [core].[PermissionMetadata] ADD [Level] int NOT NULL DEFAULT 0;

ALTER TABLE [core].[PermissionMetadata] ADD [ModuleTitle] nvarchar(128) NULL;

ALTER TABLE [core].[PermissionMetadata] ADD [NodePath] nvarchar(max) NOT NULL DEFAULT N'';

ALTER TABLE [core].[PermissionMetadata] ADD [ParentId] int NULL;

CREATE INDEX [IX_PermissionMetadata_ParentId] ON [core].[PermissionMetadata] ([ParentId]);

ALTER TABLE [core].[PermissionMetadata] ADD CONSTRAINT [FK_PermissionMetadata_PermissionMetadata_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [core].[PermissionMetadata] ([Id]) ON DELETE NO ACTION;

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260902033628_update_permissionmetadata', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
DECLARE @var3 nvarchar(max);
SELECT @var3 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[PermissionMetadata]') AND [c].[name] = N'PermissionKey');
IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [core].[PermissionMetadata] DROP CONSTRAINT ' + @var3 + ';');
ALTER TABLE [core].[PermissionMetadata] ALTER COLUMN [PermissionKey] nvarchar(128) NOT NULL;

DECLARE @var4 nvarchar(max);
SELECT @var4 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[PermissionMetadata]') AND [c].[name] = N'ModuleTitle');
IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [core].[PermissionMetadata] DROP CONSTRAINT ' + @var4 + ';');
ALTER TABLE [core].[PermissionMetadata] ALTER COLUMN [ModuleTitle] nvarchar(128) NULL;

DECLARE @var5 nvarchar(max);
SELECT @var5 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[PermissionMetadata]') AND [c].[name] = N'ModuleName');
IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [core].[PermissionMetadata] DROP CONSTRAINT ' + @var5 + ';');
ALTER TABLE [core].[PermissionMetadata] ALTER COLUMN [ModuleName] nvarchar(128) NOT NULL;

DECLARE @var6 nvarchar(max);
SELECT @var6 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[PermissionMetadata]') AND [c].[name] = N'Description');
IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [core].[PermissionMetadata] DROP CONSTRAINT ' + @var6 + ';');
ALTER TABLE [core].[PermissionMetadata] ALTER COLUMN [Description] nvarchar(256) NOT NULL;

DECLARE @var7 nvarchar(max);
SELECT @var7 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[PermissionMetadata]') AND [c].[name] = N'ControllerTitle');
IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [core].[PermissionMetadata] DROP CONSTRAINT ' + @var7 + ';');
ALTER TABLE [core].[PermissionMetadata] ALTER COLUMN [ControllerTitle] nvarchar(128) NULL;

DECLARE @var8 nvarchar(max);
SELECT @var8 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[PermissionMetadata]') AND [c].[name] = N'ControllerName');
IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [core].[PermissionMetadata] DROP CONSTRAINT ' + @var8 + ';');
ALTER TABLE [core].[PermissionMetadata] ALTER COLUMN [ControllerName] nvarchar(128) NOT NULL;

DECLARE @var9 nvarchar(max);
SELECT @var9 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[PermissionMetadata]') AND [c].[name] = N'BitPosition');
IF @var9 IS NOT NULL EXEC(N'ALTER TABLE [core].[PermissionMetadata] DROP CONSTRAINT ' + @var9 + ';');
ALTER TABLE [core].[PermissionMetadata] ALTER COLUMN [BitPosition] int NOT NULL;

DECLARE @var10 nvarchar(max);
SELECT @var10 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[PermissionMetadata]') AND [c].[name] = N'ActionName');
IF @var10 IS NOT NULL EXEC(N'ALTER TABLE [core].[PermissionMetadata] DROP CONSTRAINT ' + @var10 + ';');
ALTER TABLE [core].[PermissionMetadata] ALTER COLUMN [ActionName] nvarchar(max) NOT NULL;

DECLARE @var11 nvarchar(max);
SELECT @var11 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[PermissionMetadata]') AND [c].[name] = N'Id');
IF @var11 IS NOT NULL EXEC(N'ALTER TABLE [core].[PermissionMetadata] DROP CONSTRAINT ' + @var11 + ';');
ALTER TABLE [core].[PermissionMetadata] ALTER COLUMN [Id] int NOT NULL;

ALTER TABLE [core].[PermissionMetadata] ADD [ActionTitle] nvarchar(max) NOT NULL DEFAULT N'';

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260902070544_update_PermissionMetadata_ActionTitle', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
DROP INDEX [IX_MenuItems_DisplayOrder] ON [core].[MenuItems];

DECLARE @var12 nvarchar(max);
SELECT @var12 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[MenuItems]') AND [c].[name] = N'PermissionKey');
IF @var12 IS NOT NULL EXEC(N'ALTER TABLE [core].[MenuItems] DROP CONSTRAINT ' + @var12 + ';');
ALTER TABLE [core].[MenuItems] ALTER COLUMN [PermissionKey] nvarchar(100) NULL;

ALTER TABLE [core].[MenuItems] ADD [Component] nvarchar(200) NULL;

ALTER TABLE [core].[MenuItems] ADD [IsVisible] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [core].[MenuItems] ADD [Key] nvarchar(100) NOT NULL DEFAULT N'';

ALTER TABLE [core].[MenuItems] ADD [ModuleName] nvarchar(50) NOT NULL DEFAULT N'';

ALTER TABLE [core].[MenuItems] ADD [Type] nvarchar(20) NOT NULL DEFAULT N'';

CREATE UNIQUE INDEX [IX_MenuItems_Key] ON [core].[MenuItems] ([Key]);

CREATE INDEX [IX_MenuItems_ModuleName_IsActive_DisplayOrder] ON [core].[MenuItems] ([ModuleName], [IsActive], [DisplayOrder]);

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260907032837_update_menuitem', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
DROP INDEX [IX_Metadata_PermissionKey] ON [core].[ControllerMetadatas];
DECLARE @var13 nvarchar(max);
SELECT @var13 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[ControllerMetadatas]') AND [c].[name] = N'PermissionKey');
IF @var13 IS NOT NULL EXEC(N'ALTER TABLE [core].[ControllerMetadatas] DROP CONSTRAINT ' + @var13 + ';');
ALTER TABLE [core].[ControllerMetadatas] ALTER COLUMN [PermissionKey] nvarchar(100) NOT NULL;
CREATE INDEX [IX_Metadata_PermissionKey] ON [core].[ControllerMetadatas] ([PermissionKey]);

DECLARE @var14 nvarchar(max);
SELECT @var14 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[ControllerMetadatas]') AND [c].[name] = N'Icon');
IF @var14 IS NOT NULL EXEC(N'ALTER TABLE [core].[ControllerMetadatas] DROP CONSTRAINT ' + @var14 + ';');
ALTER TABLE [core].[ControllerMetadatas] ALTER COLUMN [Icon] nvarchar(100) NULL;

ALTER TABLE [core].[ControllerMetadatas] ADD [ControllerIcon] nvarchar(100) NULL;

ALTER TABLE [core].[ControllerMetadatas] ADD [ControllerOrder] int NOT NULL DEFAULT 0;

ALTER TABLE [core].[ControllerMetadatas] ADD [IsMenu] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [core].[ControllerMetadatas] ADD [Path] nvarchar(200) NULL;

CREATE INDEX [IX_Metadata_IsMenu] ON [core].[ControllerMetadatas] ([IsMenu]);

CREATE INDEX [IX_Metadata_IsMenu_ModuleName] ON [core].[ControllerMetadatas] ([IsMenu], [ModuleName]);

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260907050434_Update_ControllerMetadata', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE INDEX [IX_MenuItems_IsActive_IsVisible_DisplayOrder] ON [core].[MenuItems] ([IsActive], [IsVisible], [DisplayOrder]);

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260907072601_update_menuitem_20260907', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [core].[AspNetUsers] ADD [DeletedAt] datetimeoffset NULL;

ALTER TABLE [core].[AspNetUsers] ADD [DeletedBy] nvarchar(max) NOT NULL DEFAULT N'';

ALTER TABLE [core].[AspNetUsers] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260911050211_update_applicationuser_isdelete', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE INDEX [IX_UserLabMappings_User_Active_Primary] ON [core].[UserLabMappings] ([UserId], [IsActive], [IsPrimary]);

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260912101038_Update_UserMappingLab', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
DROP INDEX [IX_UserLabMappings_EffectiveRange] ON [core].[UserLabMappings];
DECLARE @var15 nvarchar(max);
SELECT @var15 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[UserLabMappings]') AND [c].[name] = N'ExpiryDate');
IF @var15 IS NOT NULL EXEC(N'ALTER TABLE [core].[UserLabMappings] DROP CONSTRAINT ' + @var15 + ';');
ALTER TABLE [core].[UserLabMappings] ALTER COLUMN [ExpiryDate] datetimeoffset(7) NULL;
CREATE INDEX [IX_UserLabMappings_EffectiveRange] ON [core].[UserLabMappings] ([UserId], [IsActive], [EffectiveDate], [ExpiryDate]);

DROP INDEX [IX_UserLabMappings_EffectiveRange] ON [core].[UserLabMappings];
DECLARE @var16 nvarchar(max);
SELECT @var16 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[UserLabMappings]') AND [c].[name] = N'EffectiveDate');
IF @var16 IS NOT NULL EXEC(N'ALTER TABLE [core].[UserLabMappings] DROP CONSTRAINT ' + @var16 + ';');
ALTER TABLE [core].[UserLabMappings] ALTER COLUMN [EffectiveDate] datetimeoffset(7) NOT NULL;
CREATE INDEX [IX_UserLabMappings_EffectiveRange] ON [core].[UserLabMappings] ([UserId], [IsActive], [EffectiveDate], [ExpiryDate]);

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260914071547_update_usermappingLab_datetimeoffset', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [core].[AspNetUsers] ADD [IsSystemAdmin] bit NOT NULL DEFAULT CAST(0 AS bit);

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260917011151_update_aalicationuser_issysamdin', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
DECLARE @var17 nvarchar(max);
SELECT @var17 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[PermissionMetadata]') AND [c].[name] = N'PermissionKey');
IF @var17 IS NOT NULL EXEC(N'ALTER TABLE [core].[PermissionMetadata] DROP CONSTRAINT ' + @var17 + ';');
ALTER TABLE [core].[PermissionMetadata] ALTER COLUMN [PermissionKey] nvarchar(128) NOT NULL;

ALTER TABLE [core].[PermissionMetadata] ADD [PermissionTitle] nvarchar(max) NOT NULL DEFAULT N'';

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260918040135_update_PermissionMetadata_permissiontitle', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
DROP INDEX [IX_PermissionMetadata_BitPosition] ON [core].[PermissionMetadata];

DROP INDEX [IX_PermissionMetadata_PermissionKey] ON [core].[PermissionMetadata];

DECLARE @var18 nvarchar(max);
SELECT @var18 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[PermissionMetadata]') AND [c].[name] = N'ParentId');
IF @var18 IS NOT NULL EXEC(N'ALTER TABLE [core].[PermissionMetadata] DROP CONSTRAINT ' + @var18 + ';');
ALTER TABLE [core].[PermissionMetadata] ALTER COLUMN [ParentId] int NULL;

DECLARE @var19 nvarchar(max);
SELECT @var19 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[PermissionMetadata]') AND [c].[name] = N'NodePath');
IF @var19 IS NOT NULL EXEC(N'ALTER TABLE [core].[PermissionMetadata] DROP CONSTRAINT ' + @var19 + ';');
ALTER TABLE [core].[PermissionMetadata] ALTER COLUMN [NodePath] nvarchar(max) NOT NULL;

DECLARE @var20 nvarchar(max);
SELECT @var20 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[PermissionMetadata]') AND [c].[name] = N'Level');
IF @var20 IS NOT NULL EXEC(N'ALTER TABLE [core].[PermissionMetadata] DROP CONSTRAINT ' + @var20 + ';');
ALTER TABLE [core].[PermissionMetadata] ALTER COLUMN [Level] int NOT NULL;

DECLARE @var21 nvarchar(max);
SELECT @var21 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[PermissionMetadata]') AND [c].[name] = N'BitPosition');
IF @var21 IS NOT NULL EXEC(N'ALTER TABLE [core].[PermissionMetadata] DROP CONSTRAINT ' + @var21 + ';');
ALTER TABLE [core].[PermissionMetadata] ALTER COLUMN [BitPosition] int NOT NULL;

DECLARE @var22 nvarchar(max);
SELECT @var22 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[PermissionMetadata]') AND [c].[name] = N'ActionName');
IF @var22 IS NOT NULL EXEC(N'ALTER TABLE [core].[PermissionMetadata] DROP CONSTRAINT ' + @var22 + ';');
ALTER TABLE [core].[PermissionMetadata] ALTER COLUMN [ActionName] nvarchar(450) NOT NULL;

CREATE UNIQUE INDEX [IX_PermissionMetadata_PermissionKey_ControllerName_ActionName] ON [core].[PermissionMetadata] ([PermissionKey], [ControllerName], [ActionName]);

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260918082247_update_PermissionMetadata_Index', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [core].[PermissionMetadata] ADD [SectionName] nvarchar(max) NOT NULL DEFAULT N'';

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260921104338_update_permissionmetadata_section', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
DECLARE @description23 AS sql_variant;
SET @description23 = N'API 版本號 (例如: v1, v2, etc.)';
EXEC sp_addextendedproperty 'MS_Description', @description23, 'SCHEMA', N'core', 'TABLE', N'ControllerMetadatas', 'COLUMN', N'Version';

DECLARE @description24 AS sql_variant;
SET @description24 = N'供前端 Axios / HttpClient 進行 HTTP 請求。';
EXEC sp_addextendedproperty 'MS_Description', @description24, 'SCHEMA', N'core', 'TABLE', N'ControllerMetadatas', 'COLUMN', N'RouteTemplate';

DECLARE @description25 AS sql_variant;
SET @description25 = N'權限Key (供前端 Axios / HttpClient 顯示)。';
EXEC sp_addextendedproperty 'MS_Description', @description25, 'SCHEMA', N'core', 'TABLE', N'ControllerMetadatas', 'COLUMN', N'PermissionKey';

DECLARE @description26 AS sql_variant;
SET @description26 = N'供前端路由系統進行頁面跳轉與選單點擊導航。';
EXEC sp_addextendedproperty 'MS_Description', @description26, 'SCHEMA', N'core', 'TABLE', N'ControllerMetadatas', 'COLUMN', N'Path';

DECLARE @description27 AS sql_variant;
SET @description27 = N'父級選單名稱 (供前端 Axios / HttpClient 顯示)。';
EXEC sp_addextendedproperty 'MS_Description', @description27, 'SCHEMA', N'core', 'TABLE', N'ControllerMetadatas', 'COLUMN', N'ParentMenuName';

DECLARE @description28 AS sql_variant;
SET @description28 = N'取自AssemblyInfo Attribute 的ModulTitle。';
EXEC sp_addextendedproperty 'MS_Description', @description28, 'SCHEMA', N'core', 'TABLE', N'ControllerMetadatas', 'COLUMN', N'ModuleTitle';

DECLARE @description29 AS sql_variant;
SET @description29 = N'取自AssemblyInfo Attribute 的ModuleName 屬性。';
EXEC sp_addextendedproperty 'MS_Description', @description29, 'SCHEMA', N'core', 'TABLE', N'ControllerMetadatas', 'COLUMN', N'ModuleName';

DECLARE @description30 AS sql_variant;
SET @description30 = N'是否為選單 (由 [Function] Attribute 之 IsMenu 標記)。';
EXEC sp_addextendedproperty 'MS_Description', @description30, 'SCHEMA', N'core', 'TABLE', N'ControllerMetadatas', 'COLUMN', N'IsMenu';

DECLARE @description31 AS sql_variant;
SET @description31 = N'標記是否啟用';
EXEC sp_addextendedproperty 'MS_Description', @description31, 'SCHEMA', N'core', 'TABLE', N'ControllerMetadatas', 'COLUMN', N'IsActive';

DECLARE @description32 AS sql_variant;
SET @description32 = N'動作圖示 (供前端 Axios / HttpClient 顯示)。';
EXEC sp_addextendedproperty 'MS_Description', @description32, 'SCHEMA', N'core', 'TABLE', N'ControllerMetadatas', 'COLUMN', N'Icon';

DECLARE @description33 AS sql_variant;
SET @description33 = N'動作排序 (供前端 Axios / HttpClient 顯示)。';
EXEC sp_addextendedproperty 'MS_Description', @description33, 'SCHEMA', N'core', 'TABLE', N'ControllerMetadatas', 'COLUMN', N'DisplayOrder';

DECLARE @description34 AS sql_variant;
SET @description34 = N'動作標題 (供前端 Axios / HttpClient 顯示)。';
EXEC sp_addextendedproperty 'MS_Description', @description34, 'SCHEMA', N'core', 'TABLE', N'ControllerMetadatas', 'COLUMN', N'DisplayName';

DECLARE @description35 AS sql_variant;
SET @description35 = N'動作說明 (供前端 Axios / HttpClient 顯示)。';
EXEC sp_addextendedproperty 'MS_Description', @description35, 'SCHEMA', N'core', 'TABLE', N'ControllerMetadatas', 'COLUMN', N'Description';

DECLARE @description36 AS sql_variant;
SET @description36 = N'建立日期';
EXEC sp_addextendedproperty 'MS_Description', @description36, 'SCHEMA', N'core', 'TABLE', N'ControllerMetadatas', 'COLUMN', N'CreatedAt';

DECLARE @description37 AS sql_variant;
SET @description37 = N'取自ControllerTypeName Atteribute 的ControllerTypeName。';
EXEC sp_addextendedproperty 'MS_Description', @description37, 'SCHEMA', N'core', 'TABLE', N'ControllerMetadatas', 'COLUMN', N'ControllerTypeName';

DECLARE @description38 AS sql_variant;
SET @description38 = N'取自ControllerTitle Atteribute 的ControllerTitle。';
EXEC sp_addextendedproperty 'MS_Description', @description38, 'SCHEMA', N'core', 'TABLE', N'ControllerMetadatas', 'COLUMN', N'ControllerTitle';

DECLARE @description39 AS sql_variant;
SET @description39 = N'取自ControllerOrder Atteribute 的ControllerOrder。';
EXEC sp_addextendedproperty 'MS_Description', @description39, 'SCHEMA', N'core', 'TABLE', N'ControllerMetadatas', 'COLUMN', N'ControllerOrder';

DECLARE @description40 AS sql_variant;
SET @description40 = N'取自ControllerName Atteribute 的ControllerName。';
EXEC sp_addextendedproperty 'MS_Description', @description40, 'SCHEMA', N'core', 'TABLE', N'ControllerMetadatas', 'COLUMN', N'ControllerName';

DECLARE @description41 AS sql_variant;
SET @description41 = N'取自ControllerIcon Atteribute 的ControllerIcon。';
EXEC sp_addextendedproperty 'MS_Description', @description41, 'SCHEMA', N'core', 'TABLE', N'ControllerMetadatas', 'COLUMN', N'ControllerIcon';

DECLARE @description42 AS sql_variant;
SET @description42 = N'該 Action 在模組內對應的位元位置 (0 ~ 63)，用於 Bitmask 快速運算。';
EXEC sp_addextendedproperty 'MS_Description', @description42, 'SCHEMA', N'core', 'TABLE', N'ControllerMetadatas', 'COLUMN', N'BitPosition';

DECLARE @description43 AS sql_variant;
SET @description43 = N' Controller 或 Action 完整 Attributes 集合 (JSON 格式)';
EXEC sp_addextendedproperty 'MS_Description', @description43, 'SCHEMA', N'core', 'TABLE', N'ControllerMetadatas', 'COLUMN', N'AttributesJson';

DECLARE @description44 AS sql_variant;
SET @description44 = N'取自ActionName Atteribute 的ActionName。';
EXEC sp_addextendedproperty 'MS_Description', @description44, 'SCHEMA', N'core', 'TABLE', N'ControllerMetadatas', 'COLUMN', N'ActionName';

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260922015413_ControllerMetadataAddColumnComment', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
DECLARE @description45 AS sql_variant;
SET @description45 = N'API 控制器與 Action 中繼資料實體';
EXEC sp_addextendedproperty 'MS_Description', @description45, 'SCHEMA', N'core', 'TABLE', N'ControllerMetadatas';

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260922020136_ControllerMetadataAddComment', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
DECLARE @var46 nvarchar(max);
SELECT @var46 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[PermissionMetadata]') AND [c].[name] = N'SectionName');
IF @var46 IS NOT NULL EXEC(N'ALTER TABLE [core].[PermissionMetadata] DROP CONSTRAINT ' + @var46 + ';');
ALTER TABLE [core].[PermissionMetadata] DROP COLUMN [SectionName];

DECLARE @description47 AS sql_variant;
SET @description47 = N'權限中繼資料實體';
EXEC sp_addextendedproperty 'MS_Description', @description47, 'SCHEMA', N'core', 'TABLE', N'PermissionMetadata';

DECLARE @var48 nvarchar(max);
SELECT @var48 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[PermissionMetadata]') AND [c].[name] = N'PermissionTitle');
IF @var48 IS NOT NULL EXEC(N'ALTER TABLE [core].[PermissionMetadata] DROP CONSTRAINT ' + @var48 + ';');
ALTER TABLE [core].[PermissionMetadata] ALTER COLUMN [PermissionTitle] nvarchar(max) NOT NULL;
DECLARE @description49 AS sql_variant;
SET @description49 = N'權限標題，對應ControllerMetadatas 的PermissionTitle。';
EXEC sp_addextendedproperty 'MS_Description', @description49, 'SCHEMA', N'core', 'TABLE', N'PermissionMetadata', 'COLUMN', N'PermissionTitle';

DECLARE @var50 nvarchar(max);
SELECT @var50 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[PermissionMetadata]') AND [c].[name] = N'PermissionKey');
IF @var50 IS NOT NULL EXEC(N'ALTER TABLE [core].[PermissionMetadata] DROP CONSTRAINT ' + @var50 + ';');
ALTER TABLE [core].[PermissionMetadata] ALTER COLUMN [PermissionKey] nvarchar(128) NOT NULL;
DECLARE @description51 AS sql_variant;
SET @description51 = N'權限代碼，對應ControllerMetadatas 的PermissionKey。';
EXEC sp_addextendedproperty 'MS_Description', @description51, 'SCHEMA', N'core', 'TABLE', N'PermissionMetadata', 'COLUMN', N'PermissionKey';

DECLARE @var52 nvarchar(max);
SELECT @var52 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[PermissionMetadata]') AND [c].[name] = N'ParentId');
IF @var52 IS NOT NULL EXEC(N'ALTER TABLE [core].[PermissionMetadata] DROP CONSTRAINT ' + @var52 + ';');
ALTER TABLE [core].[PermissionMetadata] ALTER COLUMN [ParentId] int NULL;
DECLARE @description53 AS sql_variant;
SET @description53 = N'父節點ID';
EXEC sp_addextendedproperty 'MS_Description', @description53, 'SCHEMA', N'core', 'TABLE', N'PermissionMetadata', 'COLUMN', N'ParentId';

DECLARE @var54 nvarchar(max);
SELECT @var54 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[PermissionMetadata]') AND [c].[name] = N'NodePath');
IF @var54 IS NOT NULL EXEC(N'ALTER TABLE [core].[PermissionMetadata] DROP CONSTRAINT ' + @var54 + ';');
ALTER TABLE [core].[PermissionMetadata] ALTER COLUMN [NodePath] nvarchar(max) NOT NULL;
DECLARE @description55 AS sql_variant;
SET @description55 = N'物化路徑，例如：1/2/3/4/5/';
EXEC sp_addextendedproperty 'MS_Description', @description55, 'SCHEMA', N'core', 'TABLE', N'PermissionMetadata', 'COLUMN', N'NodePath';

DECLARE @var56 nvarchar(max);
SELECT @var56 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[PermissionMetadata]') AND [c].[name] = N'ModuleTitle');
IF @var56 IS NOT NULL EXEC(N'ALTER TABLE [core].[PermissionMetadata] DROP CONSTRAINT ' + @var56 + ';');
ALTER TABLE [core].[PermissionMetadata] ALTER COLUMN [ModuleTitle] nvarchar(128) NULL;
DECLARE @description57 AS sql_variant;
SET @description57 = N'模組標題，對應ControllerMetadatas 的ModuleTitle，例如系統管理、組織管理。';
EXEC sp_addextendedproperty 'MS_Description', @description57, 'SCHEMA', N'core', 'TABLE', N'PermissionMetadata', 'COLUMN', N'ModuleTitle';

DECLARE @var58 nvarchar(max);
SELECT @var58 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[PermissionMetadata]') AND [c].[name] = N'ModuleName');
IF @var58 IS NOT NULL EXEC(N'ALTER TABLE [core].[PermissionMetadata] DROP CONSTRAINT ' + @var58 + ';');
ALTER TABLE [core].[PermissionMetadata] ALTER COLUMN [ModuleName] nvarchar(128) NOT NULL;
DECLARE @description59 AS sql_variant;
SET @description59 = N'模組名稱，對應ControllerMetadatas 的ModuleName，例如SGSFramework.System、SGSFramework.System。';
EXEC sp_addextendedproperty 'MS_Description', @description59, 'SCHEMA', N'core', 'TABLE', N'PermissionMetadata', 'COLUMN', N'ModuleName';

DECLARE @var60 nvarchar(max);
SELECT @var60 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[PermissionMetadata]') AND [c].[name] = N'Level');
IF @var60 IS NOT NULL EXEC(N'ALTER TABLE [core].[PermissionMetadata] DROP CONSTRAINT ' + @var60 + ';');
ALTER TABLE [core].[PermissionMetadata] ALTER COLUMN [Level] int NOT NULL;
DECLARE @description61 AS sql_variant;
SET @description61 = N'階層深度';
EXEC sp_addextendedproperty 'MS_Description', @description61, 'SCHEMA', N'core', 'TABLE', N'PermissionMetadata', 'COLUMN', N'Level';

DECLARE @var62 nvarchar(max);
SELECT @var62 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[PermissionMetadata]') AND [c].[name] = N'Description');
IF @var62 IS NOT NULL EXEC(N'ALTER TABLE [core].[PermissionMetadata] DROP CONSTRAINT ' + @var62 + ';');
ALTER TABLE [core].[PermissionMetadata] ALTER COLUMN [Description] nvarchar(256) NOT NULL;
DECLARE @description63 AS sql_variant;
SET @description63 = N'操作說明，對應ControllerMetadatas 的Description。';
EXEC sp_addextendedproperty 'MS_Description', @description63, 'SCHEMA', N'core', 'TABLE', N'PermissionMetadata', 'COLUMN', N'Description';

DECLARE @var64 nvarchar(max);
SELECT @var64 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[PermissionMetadata]') AND [c].[name] = N'ControllerTitle');
IF @var64 IS NOT NULL EXEC(N'ALTER TABLE [core].[PermissionMetadata] DROP CONSTRAINT ' + @var64 + ';');
ALTER TABLE [core].[PermissionMetadata] ALTER COLUMN [ControllerTitle] nvarchar(128) NULL;
DECLARE @description65 AS sql_variant;
SET @description65 = N'功能標題，對應ControllerMetadatas 的ControllerTitle。';
EXEC sp_addextendedproperty 'MS_Description', @description65, 'SCHEMA', N'core', 'TABLE', N'PermissionMetadata', 'COLUMN', N'ControllerTitle';

DECLARE @var66 nvarchar(max);
SELECT @var66 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[PermissionMetadata]') AND [c].[name] = N'ControllerName');
IF @var66 IS NOT NULL EXEC(N'ALTER TABLE [core].[PermissionMetadata] DROP CONSTRAINT ' + @var66 + ';');
ALTER TABLE [core].[PermissionMetadata] ALTER COLUMN [ControllerName] nvarchar(128) NOT NULL;
DECLARE @description67 AS sql_variant;
SET @description67 = N'功能名稱，對應ControllerMetadatas 的ControllerName。';
EXEC sp_addextendedproperty 'MS_Description', @description67, 'SCHEMA', N'core', 'TABLE', N'PermissionMetadata', 'COLUMN', N'ControllerName';

DECLARE @var68 nvarchar(max);
SELECT @var68 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[PermissionMetadata]') AND [c].[name] = N'BitPosition');
IF @var68 IS NOT NULL EXEC(N'ALTER TABLE [core].[PermissionMetadata] DROP CONSTRAINT ' + @var68 + ';');
ALTER TABLE [core].[PermissionMetadata] ALTER COLUMN [BitPosition] int NOT NULL;
DECLARE @description69 AS sql_variant;
SET @description69 = N'位址權限，對應ControllerMetadatas 的BitPosition。';
EXEC sp_addextendedproperty 'MS_Description', @description69, 'SCHEMA', N'core', 'TABLE', N'PermissionMetadata', 'COLUMN', N'BitPosition';

DECLARE @var70 nvarchar(max);
SELECT @var70 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[PermissionMetadata]') AND [c].[name] = N'ActionTitle');
IF @var70 IS NOT NULL EXEC(N'ALTER TABLE [core].[PermissionMetadata] DROP CONSTRAINT ' + @var70 + ';');
ALTER TABLE [core].[PermissionMetadata] ALTER COLUMN [ActionTitle] nvarchar(max) NOT NULL;
DECLARE @description71 AS sql_variant;
SET @description71 = N'操作標題，對應ControllerMetadatas 的ActionTitle。';
EXEC sp_addextendedproperty 'MS_Description', @description71, 'SCHEMA', N'core', 'TABLE', N'PermissionMetadata', 'COLUMN', N'ActionTitle';

DECLARE @var72 nvarchar(max);
SELECT @var72 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[PermissionMetadata]') AND [c].[name] = N'ActionName');
IF @var72 IS NOT NULL EXEC(N'ALTER TABLE [core].[PermissionMetadata] DROP CONSTRAINT ' + @var72 + ';');
ALTER TABLE [core].[PermissionMetadata] ALTER COLUMN [ActionName] nvarchar(450) NOT NULL;
DECLARE @description73 AS sql_variant;
SET @description73 = N'操作名稱，對應ControllerMetadatas 的ActionName。';
EXEC sp_addextendedproperty 'MS_Description', @description73, 'SCHEMA', N'core', 'TABLE', N'PermissionMetadata', 'COLUMN', N'ActionName';

DECLARE @var74 nvarchar(max);
SELECT @var74 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[PermissionMetadata]') AND [c].[name] = N'Id');
IF @var74 IS NOT NULL EXEC(N'ALTER TABLE [core].[PermissionMetadata] DROP CONSTRAINT ' + @var74 + ';');
ALTER TABLE [core].[PermissionMetadata] ALTER COLUMN [Id] int NOT NULL;
DECLARE @description75 AS sql_variant;
SET @description75 = N'主鍵';
EXEC sp_addextendedproperty 'MS_Description', @description75, 'SCHEMA', N'core', 'TABLE', N'PermissionMetadata', 'COLUMN', N'Id';

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260922025358_PermissionMetadataAddColumnComment', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [core].[User_Global_Permissions] ADD [CreatedAtUtc] datetimeoffset NOT NULL DEFAULT '0001-01-01T00:00:00.0000000+00:00';

ALTER TABLE [core].[User_Global_Permissions] ADD [CreatedBy] nvarchar(max) NOT NULL DEFAULT N'';

ALTER TABLE [core].[User_Global_Permissions] ADD [UpdatedAtUtc] datetimeoffset NULL;

ALTER TABLE [core].[User_Global_Permissions] ADD [UpdatedBy] nvarchar(max) NULL;

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260922153726_update_userGlobalPermission', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;

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
                

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260923040017_update_auditlog', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;

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


INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260923051148_update_core_auditlog', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
DECLARE @var76 nvarchar(max);
SELECT @var76 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[AspNetRoles]') AND [c].[name] = N'Name');
IF @var76 IS NOT NULL EXEC(N'ALTER TABLE [core].[AspNetRoles] DROP CONSTRAINT ' + @var76 + ';');
UPDATE [core].[AspNetRoles] SET [Name] = N'' WHERE [Name] IS NULL;
ALTER TABLE [core].[AspNetRoles] ALTER COLUMN [Name] nvarchar(256) NOT NULL;
ALTER TABLE [core].[AspNetRoles] ADD DEFAULT N'' FOR [Name];

DECLARE @var77 nvarchar(max);
SELECT @var77 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[AspNetRoles]') AND [c].[name] = N'Description');
IF @var77 IS NOT NULL EXEC(N'ALTER TABLE [core].[AspNetRoles] DROP CONSTRAINT ' + @var77 + ';');
ALTER TABLE [core].[AspNetRoles] ALTER COLUMN [Description] nvarchar(500) NULL;

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260923150711_update_applicationRole', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [core].[Role_Global_Permissions] (
    [id] uniqueidentifier NOT NULL,
    [role_id] varchar(450) NOT NULL,
    [permission_key] varchar(150) NOT NULL,
    [bitmask] bigint NOT NULL,
    [created_at] datetime2 NOT NULL,
    [updated_at] datetime2 NULL,
    [created_by] varchar(256) NOT NULL,
    [created_at_utc] datetimeoffset NOT NULL,
    [updated_by] varchar(256) NULL,
    [updated_at_utc] datetimeoffset NULL,
    CONSTRAINT [PK_Role_Global_Permissions] PRIMARY KEY ([id])
);

CREATE UNIQUE INDEX [ix_role_global_permissions_role_key] ON [core].[Role_Global_Permissions] ([role_id], [permission_key]);

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260924010814_role_global_permission', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [core].[User_Lab_Permissions] DROP CONSTRAINT [PK_User_Lab_Permissions];

DROP INDEX [ix_user_lab_permissions_user_lab_controller] ON [core].[User_Lab_Permissions];

ALTER TABLE [core].[User_Global_Permissions] DROP CONSTRAINT [PK_User_Global_Permissions];

DECLARE @var78 nvarchar(max);
SELECT @var78 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[User_Lab_Permissions]') AND [c].[name] = N'created_at');
IF @var78 IS NOT NULL EXEC(N'ALTER TABLE [core].[User_Lab_Permissions] DROP CONSTRAINT ' + @var78 + ';');
ALTER TABLE [core].[User_Lab_Permissions] DROP COLUMN [created_at];

DECLARE @var79 nvarchar(max);
SELECT @var79 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[User_Lab_Permissions]') AND [c].[name] = N'updated_at');
IF @var79 IS NOT NULL EXEC(N'ALTER TABLE [core].[User_Lab_Permissions] DROP CONSTRAINT ' + @var79 + ';');
ALTER TABLE [core].[User_Lab_Permissions] DROP COLUMN [updated_at];

DECLARE @var80 nvarchar(max);
SELECT @var80 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[User_Global_Permissions]') AND [c].[name] = N'created_at');
IF @var80 IS NOT NULL EXEC(N'ALTER TABLE [core].[User_Global_Permissions] DROP CONSTRAINT ' + @var80 + ';');
ALTER TABLE [core].[User_Global_Permissions] DROP COLUMN [created_at];

DECLARE @var81 nvarchar(max);
SELECT @var81 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[User_Global_Permissions]') AND [c].[name] = N'updated_at');
IF @var81 IS NOT NULL EXEC(N'ALTER TABLE [core].[User_Global_Permissions] DROP CONSTRAINT ' + @var81 + ';');
ALTER TABLE [core].[User_Global_Permissions] DROP COLUMN [updated_at];

EXEC sp_rename N'[core].[User_Lab_Permissions]', N'UserLabPermissions', 'OBJECT';

EXEC sp_rename N'[core].[User_Global_Permissions]', N'UserGlobalPermissions', 'OBJECT';

EXEC sp_rename N'[core].[UserLabPermissions].[bitmask]', N'Bitmask', 'COLUMN';

EXEC sp_rename N'[core].[UserLabPermissions].[id]', N'Id', 'COLUMN';

EXEC sp_rename N'[core].[UserLabPermissions].[user_id]', N'UserId', 'COLUMN';

EXEC sp_rename N'[core].[UserLabPermissions].[tenant_lab_id]', N'TenantLabId', 'COLUMN';

EXEC sp_rename N'[core].[UserLabPermissions].[controller_or_module_key]', N'ControllerOrModuleKey', 'COLUMN';

EXEC sp_rename N'[core].[UserLabPermissions].[ix_user_lab_permissions_user_tenant]', N'IX_UserLabPermissions_UserId_TenantLabId', 'INDEX';

EXEC sp_rename N'[core].[UserGlobalPermissions].[bitmask]', N'Bitmask', 'COLUMN';

EXEC sp_rename N'[core].[UserGlobalPermissions].[id]', N'Id', 'COLUMN';

EXEC sp_rename N'[core].[UserGlobalPermissions].[user_id]', N'UserId', 'COLUMN';

EXEC sp_rename N'[core].[UserGlobalPermissions].[permission_key]', N'PermissionKey', 'COLUMN';

EXEC sp_rename N'[core].[UserGlobalPermissions].[ix_user_global_permissions_user_key]', N'UX_UserGlobalPermissions_UserId_PermissionKey', 'INDEX';

DROP INDEX [IX_UserLabPermissions_UserId_TenantLabId] ON [core].[UserLabPermissions];
DECLARE @var82 nvarchar(max);
SELECT @var82 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[UserLabPermissions]') AND [c].[name] = N'UserId');
IF @var82 IS NOT NULL EXEC(N'ALTER TABLE [core].[UserLabPermissions] DROP CONSTRAINT ' + @var82 + ';');
ALTER TABLE [core].[UserLabPermissions] ALTER COLUMN [UserId] uniqueidentifier NOT NULL;
CREATE INDEX [IX_UserLabPermissions_UserId_TenantLabId] ON [core].[UserLabPermissions] ([UserId], [TenantLabId]);

ALTER TABLE [core].[UserLabPermissions] ADD [CreatedAtUtc] datetimeoffset(7) NOT NULL DEFAULT (SYSDATETIMEOFFSET());

ALTER TABLE [core].[UserLabPermissions] ADD [CreatedBy] nvarchar(100) NULL;

ALTER TABLE [core].[UserLabPermissions] ADD [LabId] int NOT NULL DEFAULT 0;

ALTER TABLE [core].[UserLabPermissions] ADD [UpdatedAtUtc] datetimeoffset(7) NULL;

ALTER TABLE [core].[UserLabPermissions] ADD [UpdatedBy] nvarchar(100) NULL;

DECLARE @var83 nvarchar(max);
SELECT @var83 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[UserGlobalPermissions]') AND [c].[name] = N'UpdatedBy');
IF @var83 IS NOT NULL EXEC(N'ALTER TABLE [core].[UserGlobalPermissions] DROP CONSTRAINT ' + @var83 + ';');
ALTER TABLE [core].[UserGlobalPermissions] ALTER COLUMN [UpdatedBy] nvarchar(100) NULL;

DECLARE @var84 nvarchar(max);
SELECT @var84 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[UserGlobalPermissions]') AND [c].[name] = N'UpdatedAtUtc');
IF @var84 IS NOT NULL EXEC(N'ALTER TABLE [core].[UserGlobalPermissions] DROP CONSTRAINT ' + @var84 + ';');
ALTER TABLE [core].[UserGlobalPermissions] ALTER COLUMN [UpdatedAtUtc] datetimeoffset(7) NULL;

DECLARE @var85 nvarchar(max);
SELECT @var85 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[UserGlobalPermissions]') AND [c].[name] = N'CreatedBy');
IF @var85 IS NOT NULL EXEC(N'ALTER TABLE [core].[UserGlobalPermissions] DROP CONSTRAINT ' + @var85 + ';');
ALTER TABLE [core].[UserGlobalPermissions] ALTER COLUMN [CreatedBy] nvarchar(100) NULL;

DECLARE @var86 nvarchar(max);
SELECT @var86 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[UserGlobalPermissions]') AND [c].[name] = N'CreatedAtUtc');
IF @var86 IS NOT NULL EXEC(N'ALTER TABLE [core].[UserGlobalPermissions] DROP CONSTRAINT ' + @var86 + ';');
ALTER TABLE [core].[UserGlobalPermissions] ALTER COLUMN [CreatedAtUtc] datetimeoffset(7) NOT NULL;
ALTER TABLE [core].[UserGlobalPermissions] ADD DEFAULT (SYSDATETIMEOFFSET()) FOR [CreatedAtUtc];

DROP INDEX [UX_UserGlobalPermissions_UserId_PermissionKey] ON [core].[UserGlobalPermissions];
DECLARE @var87 nvarchar(max);
SELECT @var87 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[UserGlobalPermissions]') AND [c].[name] = N'UserId');
IF @var87 IS NOT NULL EXEC(N'ALTER TABLE [core].[UserGlobalPermissions] DROP CONSTRAINT ' + @var87 + ';');
ALTER TABLE [core].[UserGlobalPermissions] ALTER COLUMN [UserId] uniqueidentifier NOT NULL;
CREATE UNIQUE INDEX [UX_UserGlobalPermissions_UserId_PermissionKey] ON [core].[UserGlobalPermissions] ([UserId], [PermissionKey]);

ALTER TABLE [core].[UserLabPermissions] ADD CONSTRAINT [PK_UserLabPermissions] PRIMARY KEY ([Id]);

ALTER TABLE [core].[UserGlobalPermissions] ADD CONSTRAINT [PK_UserGlobalPermissions] PRIMARY KEY ([Id]);

CREATE UNIQUE INDEX [UX_UserLabPermissions_User_Lab_Module] ON [core].[UserLabPermissions] ([UserId], [LabId], [ControllerOrModuleKey]);

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260925153605_user_lab_globalPermission', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [core].[PermissionMetadata] ADD [Category] nvarchar(max) NOT NULL DEFAULT N'';
DECLARE @description88 AS sql_variant;
SET @description88 = N'用於區分該節點為一般檢視、業務操作或關鍵管理權限';
EXEC sp_addextendedproperty 'MS_Description', @description88, 'SCHEMA', N'core', 'TABLE', N'PermissionMetadata', 'COLUMN', N'Category';

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260926024613_update_PermissionMetadata_ActionCategory', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
DROP INDEX [RoleNameIndex] ON [core].[AspNetRoles];

DECLARE @var89 nvarchar(max);
SELECT @var89 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[AspNetRoles]') AND [c].[name] = N'NormalizedName');
IF @var89 IS NOT NULL EXEC(N'ALTER TABLE [core].[AspNetRoles] DROP CONSTRAINT ' + @var89 + ';');
UPDATE [core].[AspNetRoles] SET [NormalizedName] = N'' WHERE [NormalizedName] IS NULL;
ALTER TABLE [core].[AspNetRoles] ALTER COLUMN [NormalizedName] nvarchar(256) NOT NULL;
ALTER TABLE [core].[AspNetRoles] ADD DEFAULT N'' FOR [NormalizedName];

DECLARE @var90 nvarchar(max);
SELECT @var90 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[AspNetRoles]') AND [c].[name] = N'Description');
IF @var90 IS NOT NULL EXEC(N'ALTER TABLE [core].[AspNetRoles] DROP CONSTRAINT ' + @var90 + ';');
ALTER TABLE [core].[AspNetRoles] ALTER COLUMN [Description] nvarchar(512) NULL;

ALTER TABLE [core].[AspNetRoles] ADD [Code] nvarchar(64) NULL;

ALTER TABLE [core].[AspNetRoles] ADD [IsSystemRole] bit NOT NULL DEFAULT CAST(0 AS bit);

CREATE UNIQUE INDEX [IX_AspNetRoles_Code] ON [core].[AspNetRoles] ([Code]) WHERE [Code] IS NOT NULL;

CREATE UNIQUE INDEX [RoleNameIndex] ON [core].[AspNetRoles] ([NormalizedName]);

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261009033553_update_approle_issystem', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [core].[AspNetUsers] ADD [EmployeeNo] nvarchar(max) NULL;

ALTER TABLE [core].[AspNetUsers] ADD [Status] nvarchar(max) NOT NULL DEFAULT N'';

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261012014519_update_applicationuser_status', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [core].[AspNetRoles] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [core].[AspNetRoles] ADD [Status] nvarchar(max) NOT NULL DEFAULT N'';

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261013015309_update_role_status', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [core].[AspNetRoles] ADD [HierarchyLevel] int NOT NULL DEFAULT 10;

ALTER TABLE [core].[AspNetRoles] ADD [ScopeType] nvarchar(max) NOT NULL DEFAULT N'LabScoped';

ALTER TABLE [core].[AspNetRoles] ADD [TenantLabId] uniqueidentifier NULL;

CREATE INDEX [IX_AspNetRoles_TenantLabId_HierarchyLevel] ON [core].[AspNetRoles] ([TenantLabId], [HierarchyLevel]);

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261015015345_update_scope', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
DROP INDEX [IX_UserRefreshTokens_RefreshTokenHash] ON [core].[UserRefreshTokens];

DROP INDEX [IX_UserRefreshTokens_UserId_DeviceId_IsDead] ON [core].[UserRefreshTokens];

CREATE INDEX [IX_UserRefreshTokens_Lockdown_Rotation_Seek] ON [core].[UserRefreshTokens] ([UserId], [DeviceId], [RefreshTokenHash], [IsDead], [IsFrozen]);

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261019060107_update_UserRefreshTokenConfiguration', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [core].[RoleAuditLogs] (
    [Id] uniqueidentifier NOT NULL,
    [RoleId] uniqueidentifier NOT NULL,
    [RoleName] nvarchar(256) NOT NULL,
    [Action] nvarchar(64) NOT NULL,
    [Summary] nvarchar(1000) NOT NULL,
    [Detail] nvarchar(max) NULL,
    [AffectedUserCount] int NOT NULL,
    [ActorUserId] uniqueidentifier NULL,
    [ActorName] nvarchar(256) NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_RoleAuditLogs] PRIMARY KEY ([Id])
);

CREATE INDEX [IX_RoleAuditLogs_RoleId_CreatedAt] ON [core].[RoleAuditLogs] ([RoleId], [CreatedAt]);

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261020035854_AddRoleAuditLogs', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [core].[UserRoleExpirations] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [RoleId] uniqueidentifier NOT NULL,
    [ExpiresAt] datetimeoffset NOT NULL,
    [Reason] nvarchar(500) NULL,
    [CreatedBy] nvarchar(256) NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_UserRoleExpirations] PRIMARY KEY ([Id])
);

CREATE INDEX [IX_UserRoleExpirations_ExpiresAt] ON [core].[UserRoleExpirations] ([ExpiresAt]);

CREATE UNIQUE INDEX [IX_UserRoleExpirations_UserId_RoleId] ON [core].[UserRoleExpirations] ([UserId], [RoleId]);

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261020040306_AddUserRoleExpirations', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [core].[RoleChangeRequests] (
    [Id] uniqueidentifier NOT NULL,
    [RoleId] uniqueidentifier NOT NULL,
    [RoleName] nvarchar(256) NOT NULL,
    [TargetPermissionKeys] nvarchar(max) NOT NULL,
    [AddedPermissionKeys] nvarchar(max) NOT NULL,
    [RemovedPermissionKeys] nvarchar(max) NOT NULL,
    [AffectedUserCount] int NOT NULL,
    [Status] nvarchar(450) NOT NULL,
    [RequestReason] nvarchar(1000) NULL,
    [RequestedBy] uniqueidentifier NOT NULL,
    [RequestedByName] nvarchar(256) NOT NULL,
    [RequestedAt] datetimeoffset NOT NULL,
    [ReviewedBy] uniqueidentifier NULL,
    [ReviewedByName] nvarchar(256) NULL,
    [ReviewedAt] datetimeoffset NULL,
    [ReviewComment] nvarchar(1000) NULL,
    CONSTRAINT [PK_RoleChangeRequests] PRIMARY KEY ([Id])
);

CREATE TABLE [core].[SodRules] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [PermissionKeyA] nvarchar(256) NOT NULL,
    [PermissionKeyB] nvarchar(256) NOT NULL,
    [IsEnabled] bit NOT NULL,
    [CreatedBy] nvarchar(256) NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_SodRules] PRIMARY KEY ([Id])
);

CREATE INDEX [IX_RoleChangeRequests_RoleId] ON [core].[RoleChangeRequests] ([RoleId]);

CREATE INDEX [IX_RoleChangeRequests_Status_RequestedAt] ON [core].[RoleChangeRequests] ([Status], [RequestedAt]);

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261020041005_AddRoleGovernance', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [core].[AccessReviewCampaigns] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [DueDate] datetimeoffset NOT NULL,
    [Status] nvarchar(450) NOT NULL,
    [RoleScope] nvarchar(max) NOT NULL,
    [CreatedBy] nvarchar(256) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [ClosedAt] datetimeoffset NULL,
    [ClosedBy] nvarchar(256) NULL,
    CONSTRAINT [PK_AccessReviewCampaigns] PRIMARY KEY ([Id])
);

CREATE TABLE [core].[AccessReviewItems] (
    [Id] uniqueidentifier NOT NULL,
    [CampaignId] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [UserName] nvarchar(256) NOT NULL,
    [FullName] nvarchar(256) NULL,
    [RoleId] uniqueidentifier NOT NULL,
    [RoleName] nvarchar(256) NOT NULL,
    [PrimaryLabId] uniqueidentifier NULL,
    [PrimaryLabName] nvarchar(256) NULL,
    [Decision] nvarchar(450) NOT NULL,
    [Comment] nvarchar(1000) NULL,
    [DecidedBy] uniqueidentifier NULL,
    [DecidedByName] nvarchar(256) NULL,
    [DecidedAt] datetimeoffset NULL,
    CONSTRAINT [PK_AccessReviewItems] PRIMARY KEY ([Id])
);

CREATE INDEX [IX_AccessReviewCampaigns_Status_CreatedAt] ON [core].[AccessReviewCampaigns] ([Status], [CreatedAt]);

CREATE INDEX [IX_AccessReviewItems_CampaignId_Decision] ON [core].[AccessReviewItems] ([CampaignId], [Decision]);

CREATE INDEX [IX_AccessReviewItems_CampaignId_PrimaryLabId] ON [core].[AccessReviewItems] ([CampaignId], [PrimaryLabId]);

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261020041406_AddAccessReview', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [core].[AspNetRoles] ADD [EffectiveFrom] datetimeoffset NULL;

ALTER TABLE [core].[AspNetRoles] ADD [EffectiveTo] datetimeoffset NULL;

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261020050534_AddRoleEffectivePeriod', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [core].[RoleChangeRequests] ADD [PreviousRoleStatus] nvarchar(max) NULL;

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261020070438_AddRoleChangeRequestPreviousStatus', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [core].[RoleLabScopes] (
    [RoleId] uniqueidentifier NOT NULL,
    [TenantLabId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_RoleLabScopes] PRIMARY KEY ([RoleId], [TenantLabId])
);

CREATE INDEX [IX_RoleLabScopes_TenantLabId] ON [core].[RoleLabScopes] ([TenantLabId]);


INSERT INTO [core].[RoleLabScopes] ([RoleId], [TenantLabId])
SELECT [Id], [TenantLabId] FROM [core].[AspNetRoles]
WHERE [TenantLabId] IS NOT NULL AND [TenantLabId] <> '00000000-0000-0000-0000-000000000000';

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261020074120_AddRoleLabScopes', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [core].[LabFunctionPolicies] (
    [TenantLabId] uniqueidentifier NOT NULL,
    [IsRestricted] bit NOT NULL,
    [UpdatedAt] datetimeoffset NOT NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [UpdatedByName] nvarchar(256) NULL,
    CONSTRAINT [PK_LabFunctionPolicies] PRIMARY KEY ([TenantLabId])
);

CREATE TABLE [core].[LabFunctionScopes] (
    [TenantLabId] uniqueidentifier NOT NULL,
    [PermissionKey] nvarchar(256) NOT NULL,
    CONSTRAINT [PK_LabFunctionScopes] PRIMARY KEY ([TenantLabId], [PermissionKey])
);

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261020083152_AddLabFunctionScopes', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [core].[LabManagers] (
    [TenantLabId] uniqueidentifier NOT NULL,
    [ManagerUserId] uniqueidentifier NOT NULL,
    [UpdatedAt] datetimeoffset NOT NULL,
    [UpdatedByName] nvarchar(256) NULL,
    CONSTRAINT [PK_LabManagers] PRIMARY KEY ([TenantLabId])
);

CREATE INDEX [IX_LabManagers_ManagerUserId] ON [core].[LabManagers] ([ManagerUserId]);

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261020084459_AddLabManagers', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [core].[AspNetRoles] ADD [RequiresAuditAssignment] bit NOT NULL DEFAULT CAST(0 AS bit);

CREATE TABLE [core].[LabAuditAssignments] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [TenantLabId] uniqueidentifier NOT NULL,
    [EffectiveFrom] datetimeoffset NOT NULL,
    [EffectiveTo] datetimeoffset NOT NULL,
    [Reason] nvarchar(500) NOT NULL,
    [AssignedBy] uniqueidentifier NULL,
    [AssignedByName] nvarchar(256) NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [RevokedAt] datetimeoffset NULL,
    [RevokedByName] nvarchar(256) NULL,
    [RevokeReason] nvarchar(500) NULL,
    CONSTRAINT [PK_LabAuditAssignments] PRIMARY KEY ([Id])
);

CREATE INDEX [IX_LabAuditAssignments_TenantLabId] ON [core].[LabAuditAssignments] ([TenantLabId]);

CREATE INDEX [IX_LabAuditAssignments_UserId_TenantLabId] ON [core].[LabAuditAssignments] ([UserId], [TenantLabId]);


UPDATE [core].[AspNetRoles] SET [RequiresAuditAssignment] = 1
WHERE [Code] = 'AU' AND [IsDeleted] = 0;

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261022112353_AddLabAuditAssignments', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [core].[ControllerMetadatas] ADD [IsManuallyDisabled] bit NOT NULL DEFAULT CAST(0 AS bit);
DECLARE @description91 AS sql_variant;
SET @description91 = N'是否由管理者手動停用；啟動同步或模組重新啟用時不會覆蓋為啟用。';
EXEC sp_addextendedproperty 'MS_Description', @description91, 'SCHEMA', N'core', 'TABLE', N'ControllerMetadatas', 'COLUMN', N'IsManuallyDisabled';

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261022160726_ControllerMetadata_IsManuallyDisabled', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [core].[ControllerMetadatas] ADD [DisabledAt] datetimeoffset NULL;
DECLARE @description92 AS sql_variant;
SET @description92 = N'手動停用的時間 (UTC)。';
EXEC sp_addextendedproperty 'MS_Description', @description92, 'SCHEMA', N'core', 'TABLE', N'ControllerMetadatas', 'COLUMN', N'DisabledAt';

ALTER TABLE [core].[ControllerMetadatas] ADD [DisabledNotice] nvarchar(500) NULL;
DECLARE @description93 AS sql_variant;
SET @description93 = N'手動停用時對使用者的公告內容。';
EXEC sp_addextendedproperty 'MS_Description', @description93, 'SCHEMA', N'core', 'TABLE', N'ControllerMetadatas', 'COLUMN', N'DisabledNotice';

ALTER TABLE [core].[ControllerMetadatas] ADD [ExpectedRestoreAt] datetimeoffset NULL;
DECLARE @description94 AS sql_variant;
SET @description94 = N'手動停用時公告的預計恢復時間 (UTC)，僅供公告不會自動恢復。';
EXEC sp_addextendedproperty 'MS_Description', @description94, 'SCHEMA', N'core', 'TABLE', N'ControllerMetadatas', 'COLUMN', N'ExpectedRestoreAt';

CREATE TABLE [core].[FunctionStatusLogs] (
    [Id] uniqueidentifier NOT NULL,
    [ControllerMetadataId] uniqueidentifier NOT NULL,
    [ControllerName] nvarchar(100) NOT NULL,
    [ActionName] nvarchar(100) NOT NULL,
    [DisplayName] nvarchar(100) NOT NULL,
    [PermissionKey] nvarchar(100) NOT NULL,
    [IsActive] bit NOT NULL,
    [Reason] nvarchar(500) NULL,
    [Notice] nvarchar(500) NULL,
    [ExpectedRestoreAt] datetimeoffset NULL,
    [ActorUserId] uniqueidentifier NULL,
    [ActorName] nvarchar(256) NULL,
    [ClientIp] nvarchar(64) NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_FunctionStatusLogs] PRIMARY KEY ([Id])
);

CREATE INDEX [IX_FunctionStatusLogs_ControllerMetadataId_CreatedAt] ON [core].[FunctionStatusLogs] ([ControllerMetadataId], [CreatedAt]);

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261023014530_FunctionStatus_NoticeAndAuditLog', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [core].[SystemSettingChangeLogs] (
    [Id] uniqueidentifier NOT NULL,
    [SettingKey] nvarchar(100) NOT NULL,
    [Summary] nvarchar(2000) NOT NULL,
    [ActorUserId] uniqueidentifier NULL,
    [ActorName] nvarchar(256) NULL,
    [ClientIp] nvarchar(64) NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_SystemSettingChangeLogs] PRIMARY KEY ([Id])
);

CREATE TABLE [core].[SystemSettings] (
    [Key] nvarchar(100) NOT NULL,
    [Value] nvarchar(max) NOT NULL,
    [UpdatedByUserId] uniqueidentifier NULL,
    [UpdatedByName] nvarchar(256) NULL,
    [UpdatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_SystemSettings] PRIMARY KEY ([Key])
);

CREATE INDEX [IX_SystemSettingChangeLogs_CreatedAt] ON [core].[SystemSettingChangeLogs] ([CreatedAt]);

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261023135734_AddSystemSettings', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
DECLARE @description95 AS sql_variant;
SET @description95 = N'過渡期角色（Transition Role）：角色指派的有效期限。調職交接時保留舊角色一段時間，到期由排程自動回收，避免權限累積 (Privilege Creep)。每位使用者在同一角色上最多一筆未處理的期限。';
EXEC sp_addextendedproperty 'MS_Description', @description95, 'SCHEMA', N'core', 'TABLE', N'UserRoleExpirations';

DECLARE @description96 AS sql_variant;
SET @description96 = N'使用者資源授權實體，表示特定使用者對特定資源（如功能表項目）的存取權限';
EXEC sp_addextendedproperty 'MS_Description', @description96, 'SCHEMA', N'core', 'TABLE', N'UserResourceGrants';

DECLARE @description97 AS sql_variant;
SET @description97 = N'使用者實驗室專屬 64 位元遮罩權限實體 (對齊 UserLabMapping 型別與稽核標準)';
EXEC sp_addextendedproperty 'MS_Description', @description97, 'SCHEMA', N'core', 'TABLE', N'UserLabPermissions';

DECLARE @description98 AS sql_variant;
SET @description98 = N'用戶與實驗室映射（管理用戶與實驗室之間的關聯，包括用戶的主要實驗室和職位標題等信息）';
EXEC sp_addextendedproperty 'MS_Description', @description98, 'SCHEMA', N'core', 'TABLE', N'UserLabMappings';

DECLARE @description99 AS sql_variant;
SET @description99 = N'使用者全域/組織級 64 位元遮罩權限實體 (統一 UserId 為 Guid 型別)';
EXEC sp_addextendedproperty 'MS_Description', @description99, 'SCHEMA', N'core', 'TABLE', N'UserGlobalPermissions';

DECLARE @description100 AS sql_variant;
SET @description100 = N'系統配置（管理者可於執行期調整的設定，例如外觀識別、密碼規則）。以「分類鍵 + JSON 值」儲存，新增設定分類時不需異動資料表結構。';
EXEC sp_addextendedproperty 'MS_Description', @description100, 'SCHEMA', N'core', 'TABLE', N'SystemSettings';

DECLARE @description101 AS sql_variant;
SET @description101 = N'系統配置異動歷程（稽核用，僅新增不修改）';
EXEC sp_addextendedproperty 'MS_Description', @description101, 'SCHEMA', N'core', 'TABLE', N'SystemSettingChangeLogs';

DECLARE @description102 AS sql_variant;
SET @description102 = N'系統日誌實體：用於記錄系統運行狀態、事件、錯誤與稽核資訊';
EXEC sp_addextendedproperty 'MS_Description', @description102, 'SCHEMA', N'core', 'TABLE', N'SystemLogs';

DECLARE @description103 AS sql_variant;
SET @description103 = N'安全性審計總帳實體模型 (對應 MSSQL 2025 Ledger 防篡改唯讀資料表)';
EXEC sp_addextendedproperty 'MS_Description', @description103, 'SCHEMA', N'core', 'TABLE', N'SecurityLog';

DECLARE @description104 AS sql_variant;
SET @description104 = N'實驗室專屬角色適用的區域實驗室（一個角色可指派多個實驗室）';
EXEC sp_addextendedproperty 'MS_Description', @description104, 'SCHEMA', N'core', 'TABLE', N'RoleLabScopes';

DECLARE @description105 AS sql_variant;
SET @description105 = N'角色權限變更申請，用於管理角色權限的變更流程。';
EXEC sp_addextendedproperty 'MS_Description', @description105, 'SCHEMA', N'core', 'TABLE', N'RoleChangeRequests';

DECLARE @description106 AS sql_variant;
SET @description106 = N'角色異動歷程（稽核用，僅新增不修改）。記錄角色狀態、設定、權限、成員、AD 群組等所有異動，含操作者、時間與影響人數。';
EXEC sp_addextendedproperty 'MS_Description', @description106, 'SCHEMA', N'core', 'TABLE', N'RoleAuditLogs';

DECLARE @description107 AS sql_variant;
SET @description107 = N'角色全域/模組級 64 位元遮罩權限實體';
EXEC sp_addextendedproperty 'MS_Description', @description107, 'SCHEMA', N'core', 'TABLE', N'Role_Global_Permissions';

DECLARE @description108 AS sql_variant;
SET @description108 = N'身分補償修復憑證實體：專門用於記錄因資安熔斷（Lockdown）後，允許用戶執行高風險密碼修復或身分重驗的臨時安全 Ticket。';
EXEC sp_addextendedproperty 'MS_Description', @description108, 'SCHEMA', N'core', 'TABLE', N'RemediationTickets';

DECLARE @description109 AS sql_variant;
SET @description109 = N'角色權限授權實體，表示特定角色對特定功能或資源的存取權限。';
EXEC sp_addextendedproperty 'MS_Description', @description109, 'SCHEMA', N'core', 'TABLE', N'PermissionGrants';

DECLARE @description110 AS sql_variant;
SET @description110 = N'模組元資料實體，包含模組的基本資訊與狀態';
EXEC sp_addextendedproperty 'MS_Description', @description110, 'SCHEMA', N'core', 'TABLE', N'ModuleMetadatas';

DECLARE @description111 AS sql_variant;
SET @description111 = N'動態選單實體 (Domain Entity)';
EXEC sp_addextendedproperty 'MS_Description', @description111, 'SCHEMA', N'core', 'TABLE', N'MenuItems';

DECLARE @description112 AS sql_variant;
SET @description112 = N'區域實驗室主管（以 TenantLabId 對應 ORG 區域實驗室，與 UserLabMapping 相同的鍵值維度）';
EXEC sp_addextendedproperty 'MS_Description', @description112, 'SCHEMA', N'core', 'TABLE', N'LabManagers';

DECLARE @description113 AS sql_variant;
SET @description113 = N'區域實驗室開放的權限（完整權限鍵，例：ORG.LABORATORY.READ）';
EXEC sp_addextendedproperty 'MS_Description', @description113, 'SCHEMA', N'core', 'TABLE', N'LabFunctionScopes';

DECLARE @description114 AS sql_variant;
SET @description114 = N'區域實驗室的功能開放政策。IsRestricted = false（或無資料）表示所有功能開放；IsRestricted = true 時，僅 LabFunctionScope 列出的權限於該實驗室生效（對所有使用者與角色的上限，系統管理員除外）。';
EXEC sp_addextendedproperty 'MS_Description', @description114, 'SCHEMA', N'core', 'TABLE', N'LabFunctionPolicies';

DECLARE @description115 AS sql_variant;
SET @description115 = N'實驗室稽核指派：指定使用者於起訖期間內對某區域實驗室執行稽核職責。有效期間內，使用者所持有的稽核角色（ApplicationRole.RequiresAuditAssignment）權限於該實驗室生效，且該實驗室會出現在使用者可切換的實驗室清單中；期間外或撤銷後自動失效。紀錄不刪除，保留完整稽核軌跡。';
EXEC sp_addextendedproperty 'MS_Description', @description115, 'SCHEMA', N'core', 'TABLE', N'LabAuditAssignments';

DECLARE @description116 AS sql_variant;
SET @description116 = N'系統功能啟用 / 停用異動歷程（稽核用，僅新增不修改）';
EXEC sp_addextendedproperty 'MS_Description', @description116, 'SCHEMA', N'core', 'TABLE', N'FunctionStatusLogs';

DECLARE @description117 AS sql_variant;
SET @description117 = N'資料稽核日誌實體';
EXEC sp_addextendedproperty 'MS_Description', @description117, 'SCHEMA', N'core', 'TABLE', N'AuditLogs';

DECLARE @description118 AS sql_variant;
SET @description118 = N'擴充自訂的 Identity 使用者實體';
EXEC sp_addextendedproperty 'MS_Description', @description118, 'SCHEMA', N'core', 'TABLE', N'AspNetUsers';

DECLARE @description119 AS sql_variant;
SET @description119 = N'企業級自訂應用程式角色實體 (支援 ARBAC 與多實驗室隔離)';
EXEC sp_addextendedproperty 'MS_Description', @description119, 'SCHEMA', N'core', 'TABLE', N'AspNetRoles';

DECLARE @description120 AS sql_variant;
SET @description120 = N'覆核項目：一位使用者在一個角色上的指派';
EXEC sp_addextendedproperty 'MS_Description', @description120, 'SCHEMA', N'core', 'TABLE', N'AccessReviewItems';

DECLARE @description121 AS sql_variant;
SET @description121 = N'定期權限覆核活動 (Access Certification Campaign)。建立時快照當下所有（或指定角色的）成員指派，由覆核者逐一確認「保留」或「回收」。';
EXEC sp_addextendedproperty 'MS_Description', @description121, 'SCHEMA', N'core', 'TABLE', N'AccessReviewCampaigns';

DECLARE @description122 AS sql_variant;
SET @description122 = N'使用者識別鍵';
EXEC sp_addextendedproperty 'MS_Description', @description122, 'SCHEMA', N'core', 'TABLE', N'UserRoleExpirations', 'COLUMN', N'UserId';

DECLARE @description123 AS sql_variant;
SET @description123 = N'角色識別鍵';
EXEC sp_addextendedproperty 'MS_Description', @description123, 'SCHEMA', N'core', 'TABLE', N'UserRoleExpirations', 'COLUMN', N'RoleId';

DECLARE @description124 AS sql_variant;
SET @description124 = N'設定原因，例如「調任銷售部，業務交接」';
EXEC sp_addextendedproperty 'MS_Description', @description124, 'SCHEMA', N'core', 'TABLE', N'UserRoleExpirations', 'COLUMN', N'Reason';

DECLARE @description125 AS sql_variant;
SET @description125 = N'到期時間（UTC），到期後由排程自動解除角色指派';
EXEC sp_addextendedproperty 'MS_Description', @description125, 'SCHEMA', N'core', 'TABLE', N'UserRoleExpirations', 'COLUMN', N'ExpiresAt';

DECLARE @description126 AS sql_variant;
SET @description126 = N'建立者';
EXEC sp_addextendedproperty 'MS_Description', @description126, 'SCHEMA', N'core', 'TABLE', N'UserRoleExpirations', 'COLUMN', N'CreatedBy';

DECLARE @description127 AS sql_variant;
SET @description127 = N'建立時間';
EXEC sp_addextendedproperty 'MS_Description', @description127, 'SCHEMA', N'core', 'TABLE', N'UserRoleExpirations', 'COLUMN', N'CreatedAt';

DECLARE @description128 AS sql_variant;
SET @description128 = N'識別主鍵';
EXEC sp_addextendedproperty 'MS_Description', @description128, 'SCHEMA', N'core', 'TABLE', N'UserRoleExpirations', 'COLUMN', N'Id';

DECLARE @description129 AS sql_variant;
SET @description129 = N'有效結束時間';
EXEC sp_addextendedproperty 'MS_Description', @description129, 'SCHEMA', N'core', 'TABLE', N'UserResourceGrants', 'COLUMN', N'ValidTo';

DECLARE @description130 AS sql_variant;
SET @description130 = N'有效起始時間';
EXEC sp_addextendedproperty 'MS_Description', @description130, 'SCHEMA', N'core', 'TABLE', N'UserResourceGrants', 'COLUMN', N'ValidFrom';

DECLARE @description131 AS sql_variant;
SET @description131 = N'使用者識別鍵';
EXEC sp_addextendedproperty 'MS_Description', @description131, 'SCHEMA', N'core', 'TABLE', N'UserResourceGrants', 'COLUMN', N'UserId';

DECLARE @description132 AS sql_variant;
SET @description132 = N'權限類型';
EXEC sp_addextendedproperty 'MS_Description', @description132, 'SCHEMA', N'core', 'TABLE', N'UserResourceGrants', 'COLUMN', N'PermissionType';

DECLARE @description133 AS sql_variant;
SET @description133 = N'功能表項目識別鍵';
EXEC sp_addextendedproperty 'MS_Description', @description133, 'SCHEMA', N'core', 'TABLE', N'UserResourceGrants', 'COLUMN', N'MenuItemId';

DECLARE @description134 AS sql_variant;
SET @description134 = N'實驗室識別鍵';
EXEC sp_addextendedproperty 'MS_Description', @description134, 'SCHEMA', N'core', 'TABLE', N'UserResourceGrants', 'COLUMN', N'LabId';

DECLARE @description135 AS sql_variant;
SET @description135 = N'權限效果';
EXEC sp_addextendedproperty 'MS_Description', @description135, 'SCHEMA', N'core', 'TABLE', N'UserResourceGrants', 'COLUMN', N'EffectType';

DECLARE @description136 AS sql_variant;
SET @description136 = N'被代理人識別鍵';
EXEC sp_addextendedproperty 'MS_Description', @description136, 'SCHEMA', N'core', 'TABLE', N'UserResourceGrants', 'COLUMN', N'DelegatorUserId';

DECLARE @description137 AS sql_variant;
SET @description137 = N'識別主鍵';
EXEC sp_addextendedproperty 'MS_Description', @description137, 'SCHEMA', N'core', 'TABLE', N'UserResourceGrants', 'COLUMN', N'Id';

DECLARE @description138 AS sql_variant;
SET @description138 = N'使用者唯一識別碼';
EXEC sp_addextendedproperty 'MS_Description', @description138, 'SCHEMA', N'core', 'TABLE', N'UserRefreshTokens', 'COLUMN', N'UserId';

DECLARE @description139 AS sql_variant;
SET @description139 = N'上一次成功輪換的時間（用於寬限期計算）';
EXEC sp_addextendedproperty 'MS_Description', @description139, 'SCHEMA', N'core', 'TABLE', N'UserRefreshTokens', 'COLUMN', N'RotatedAt';

DECLARE @description140 AS sql_variant;
SET @description140 = N'記錄風險觸發原因或主動登出/熔斷稽核事由';
EXEC sp_addextendedproperty 'MS_Description', @description140, 'SCHEMA', N'core', 'TABLE', N'UserRefreshTokens', 'COLUMN', N'RiskReason';

DECLARE @description141 AS sql_variant;
SET @description141 = N'快取上一次生成的全新 TokenHash，供寬限期內的併發請求直接複用';
EXEC sp_addextendedproperty 'MS_Description', @description141, 'SCHEMA', N'core', 'TABLE', N'UserRefreshTokens', 'COLUMN', N'ReusedTokenCache';

DECLARE @description142 AS sql_variant;
SET @description142 = N'該裝置最後一次發起請求或換票的活動時間 (用於 LIFO 被動擠出策略排序)';
EXEC sp_addextendedproperty 'MS_Description', @description142, 'SCHEMA', N'core', 'TABLE', N'UserRefreshTokens', 'COLUMN', N'LastActiveAt';

DECLARE @description143 AS sql_variant;
SET @description143 = N'標記該用戶所有/特定 Session 是否被安全鎖定';
EXEC sp_addextendedproperty 'MS_Description', @description143, 'SCHEMA', N'core', 'TABLE', N'UserRefreshTokens', 'COLUMN', N'IsFrozen';

DECLARE @description144 AS sql_variant;
SET @description144 = N'核心資安防禦熔斷標記：標記該憑證是否已被重放攻擊戳死、或已被主動註銷失效';
EXEC sp_addextendedproperty 'MS_Description', @description144, 'SCHEMA', N'core', 'TABLE', N'UserRefreshTokens', 'COLUMN', N'IsDead';

DECLARE @description145 AS sql_variant;
SET @description145 = N'憑證絕對過期時間';
EXEC sp_addextendedproperty 'MS_Description', @description145, 'SCHEMA', N'core', 'TABLE', N'UserRefreshTokens', 'COLUMN', N'ExpiresAt';

DECLARE @description146 AS sql_variant;
SET @description146 = N'裝置名稱或瀏覽器特徵描述 (例如: Chrome on Windows)';
EXEC sp_addextendedproperty 'MS_Description', @description146, 'SCHEMA', N'core', 'TABLE', N'UserRefreshTokens', 'COLUMN', N'DeviceName';

DECLARE @description147 AS sql_variant;
SET @description147 = N'裝置唯一指紋 (由前端 Vue / Blazor 經由 JS 產生的不重複雜湊值)';
EXEC sp_addextendedproperty 'MS_Description', @description147, 'SCHEMA', N'core', 'TABLE', N'UserRefreshTokens', 'COLUMN', N'DeviceId';

DECLARE @description148 AS sql_variant;
SET @description148 = N'建立時間';
EXEC sp_addextendedproperty 'MS_Description', @description148, 'SCHEMA', N'core', 'TABLE', N'UserRefreshTokens', 'COLUMN', N'CreatedAt';

DECLARE @description149 AS sql_variant;
SET @description149 = N'登入時的來源 IP 紀錄';
EXEC sp_addextendedproperty 'MS_Description', @description149, 'SCHEMA', N'core', 'TABLE', N'UserRefreshTokens', 'COLUMN', N'ClientIp';

DECLARE @description150 AS sql_variant;
SET @description150 = N'識別鍵';
EXEC sp_addextendedproperty 'MS_Description', @description150, 'SCHEMA', N'core', 'TABLE', N'UserRefreshTokens', 'COLUMN', N'Id';

DECLARE @description151 AS sql_variant;
SET @description151 = N'使用者識別鍵';
EXEC sp_addextendedproperty 'MS_Description', @description151, 'SCHEMA', N'core', 'TABLE', N'UserLabPermissions', 'COLUMN', N'UserId';

DECLARE @description152 AS sql_variant;
SET @description152 = N'最後修改者';
EXEC sp_addextendedproperty 'MS_Description', @description152, 'SCHEMA', N'core', 'TABLE', N'UserLabPermissions', 'COLUMN', N'UpdatedBy';

DECLARE @description153 AS sql_variant;
SET @description153 = N'最後修改時間 (UTC)';
EXEC sp_addextendedproperty 'MS_Description', @description153, 'SCHEMA', N'core', 'TABLE', N'UserLabPermissions', 'COLUMN', N'UpdatedAtUtc';

DECLARE @description154 AS sql_variant;
SET @description154 = N'區域實驗室識別鍵';
EXEC sp_addextendedproperty 'MS_Description', @description154, 'SCHEMA', N'core', 'TABLE', N'UserLabPermissions', 'COLUMN', N'TenantLabId';

DECLARE @description155 AS sql_variant;
SET @description155 = N'實驗室識別鍵';
EXEC sp_addextendedproperty 'MS_Description', @description155, 'SCHEMA', N'core', 'TABLE', N'UserLabPermissions', 'COLUMN', N'LabId';

DECLARE @description156 AS sql_variant;
SET @description156 = N'建立者';
EXEC sp_addextendedproperty 'MS_Description', @description156, 'SCHEMA', N'core', 'TABLE', N'UserLabPermissions', 'COLUMN', N'CreatedBy';

DECLARE @description157 AS sql_variant;
SET @description157 = N'建立時間 (UTC)';
EXEC sp_addextendedproperty 'MS_Description', @description157, 'SCHEMA', N'core', 'TABLE', N'UserLabPermissions', 'COLUMN', N'CreatedAtUtc';

DECLARE @description158 AS sql_variant;
SET @description158 = N'控制器或模組識別鍵';
EXEC sp_addextendedproperty 'MS_Description', @description158, 'SCHEMA', N'core', 'TABLE', N'UserLabPermissions', 'COLUMN', N'ControllerOrModuleKey';

DECLARE @description159 AS sql_variant;
SET @description159 = N'64 位元權限遮罩值 (Bitmask 0-63)';
EXEC sp_addextendedproperty 'MS_Description', @description159, 'SCHEMA', N'core', 'TABLE', N'UserLabPermissions', 'COLUMN', N'Bitmask';

DECLARE @description160 AS sql_variant;
SET @description160 = N'識別主鍵';
EXEC sp_addextendedproperty 'MS_Description', @description160, 'SCHEMA', N'core', 'TABLE', N'UserLabPermissions', 'COLUMN', N'Id';

DECLARE @description161 AS sql_variant;
SET @description161 = N'最後更新時間';
EXEC sp_addextendedproperty 'MS_Description', @description161, 'SCHEMA', N'core', 'TABLE', N'UserLabMappings', 'COLUMN', N'UpdatedAtUtc';

DECLARE @description162 AS sql_variant;
SET @description162 = N'區域實驗室識別鍵';
EXEC sp_addextendedproperty 'MS_Description', @description162, 'SCHEMA', N'core', 'TABLE', N'UserLabMappings', 'COLUMN', N'TenantLabId';

DECLARE @description163 AS sql_variant;
SET @description163 = N'職位標題';
EXEC sp_addextendedproperty 'MS_Description', @description163, 'SCHEMA', N'core', 'TABLE', N'UserLabMappings', 'COLUMN', N'JobTitle';

DECLARE @description164 AS sql_variant;
SET @description164 = N'是否為主要實驗室';
EXEC sp_addextendedproperty 'MS_Description', @description164, 'SCHEMA', N'core', 'TABLE', N'UserLabMappings', 'COLUMN', N'IsPrimary';

DECLARE @description165 AS sql_variant;
SET @description165 = N'是否啟用';
EXEC sp_addextendedproperty 'MS_Description', @description165, 'SCHEMA', N'core', 'TABLE', N'UserLabMappings', 'COLUMN', N'IsActive';

DECLARE @description166 AS sql_variant;
SET @description166 = N'失效日期';
EXEC sp_addextendedproperty 'MS_Description', @description166, 'SCHEMA', N'core', 'TABLE', N'UserLabMappings', 'COLUMN', N'ExpiryDate';

DECLARE @description167 AS sql_variant;
SET @description167 = N'生效日期';
EXEC sp_addextendedproperty 'MS_Description', @description167, 'SCHEMA', N'core', 'TABLE', N'UserLabMappings', 'COLUMN', N'EffectiveDate';

DECLARE @description168 AS sql_variant;
SET @description168 = N'建立人員識別鍵';
EXEC sp_addextendedproperty 'MS_Description', @description168, 'SCHEMA', N'core', 'TABLE', N'UserLabMappings', 'COLUMN', N'CreatedBy';

DECLARE @description169 AS sql_variant;
SET @description169 = N'建立時間';
EXEC sp_addextendedproperty 'MS_Description', @description169, 'SCHEMA', N'core', 'TABLE', N'UserLabMappings', 'COLUMN', N'CreatedAtUtc';

DECLARE @description170 AS sql_variant;
SET @description170 = N'實驗室識別鍵';
EXEC sp_addextendedproperty 'MS_Description', @description170, 'SCHEMA', N'core', 'TABLE', N'UserLabMappings', 'COLUMN', N'LabId';

DECLARE @description171 AS sql_variant;
SET @description171 = N'使用者識別鍵';
EXEC sp_addextendedproperty 'MS_Description', @description171, 'SCHEMA', N'core', 'TABLE', N'UserLabMappings', 'COLUMN', N'UserId';

DECLARE @description172 AS sql_variant;
SET @description172 = N'使用者識別鍵';
EXEC sp_addextendedproperty 'MS_Description', @description172, 'SCHEMA', N'core', 'TABLE', N'UserGlobalPermissions', 'COLUMN', N'UserId';

DECLARE @description173 AS sql_variant;
SET @description173 = N'權限識別鍵';
EXEC sp_addextendedproperty 'MS_Description', @description173, 'SCHEMA', N'core', 'TABLE', N'UserGlobalPermissions', 'COLUMN', N'PermissionKey';

DECLARE @description174 AS sql_variant;
SET @description174 = N'64 位元權限遮罩值 (Bitmask 0-63)';
EXEC sp_addextendedproperty 'MS_Description', @description174, 'SCHEMA', N'core', 'TABLE', N'UserGlobalPermissions', 'COLUMN', N'Bitmask';

DECLARE @description175 AS sql_variant;
SET @description175 = N'識別主鍵';
EXEC sp_addextendedproperty 'MS_Description', @description175, 'SCHEMA', N'core', 'TABLE', N'UserGlobalPermissions', 'COLUMN', N'Id';

DECLARE @description176 AS sql_variant;
SET @description176 = N'設定內容（JSON）';
EXEC sp_addextendedproperty 'MS_Description', @description176, 'SCHEMA', N'core', 'TABLE', N'SystemSettings', 'COLUMN', N'Value';

DECLARE @description177 AS sql_variant;
SET @description177 = N'更新者用戶識別鍵';
EXEC sp_addextendedproperty 'MS_Description', @description177, 'SCHEMA', N'core', 'TABLE', N'SystemSettings', 'COLUMN', N'UpdatedByUserId';

DECLARE @description178 AS sql_variant;
SET @description178 = N'更新者名稱';
EXEC sp_addextendedproperty 'MS_Description', @description178, 'SCHEMA', N'core', 'TABLE', N'SystemSettings', 'COLUMN', N'UpdatedByName';

DECLARE @description179 AS sql_variant;
SET @description179 = N'更新時間';
EXEC sp_addextendedproperty 'MS_Description', @description179, 'SCHEMA', N'core', 'TABLE', N'SystemSettings', 'COLUMN', N'UpdatedAt';

DECLARE @description180 AS sql_variant;
SET @description180 = N'識別主鍵';
EXEC sp_addextendedproperty 'MS_Description', @description180, 'SCHEMA', N'core', 'TABLE', N'SystemSettings', 'COLUMN', N'Key';

DECLARE @description181 AS sql_variant;
SET @description181 = N'異動摘要';
EXEC sp_addextendedproperty 'MS_Description', @description181, 'SCHEMA', N'core', 'TABLE', N'SystemSettingChangeLogs', 'COLUMN', N'Summary';

DECLARE @description182 AS sql_variant;
SET @description182 = N'設定分類鍵';
EXEC sp_addextendedproperty 'MS_Description', @description182, 'SCHEMA', N'core', 'TABLE', N'SystemSettingChangeLogs', 'COLUMN', N'SettingKey';

DECLARE @description183 AS sql_variant;
SET @description183 = N'建立時間';
EXEC sp_addextendedproperty 'MS_Description', @description183, 'SCHEMA', N'core', 'TABLE', N'SystemSettingChangeLogs', 'COLUMN', N'CreatedAt';

DECLARE @description184 AS sql_variant;
SET @description184 = N'客戶端 IP';
EXEC sp_addextendedproperty 'MS_Description', @description184, 'SCHEMA', N'core', 'TABLE', N'SystemSettingChangeLogs', 'COLUMN', N'ClientIp';

DECLARE @description185 AS sql_variant;
SET @description185 = N'操作者用戶識別鍵';
EXEC sp_addextendedproperty 'MS_Description', @description185, 'SCHEMA', N'core', 'TABLE', N'SystemSettingChangeLogs', 'COLUMN', N'ActorUserId';

DECLARE @description186 AS sql_variant;
SET @description186 = N'操作者名稱';
EXEC sp_addextendedproperty 'MS_Description', @description186, 'SCHEMA', N'core', 'TABLE', N'SystemSettingChangeLogs', 'COLUMN', N'ActorName';

DECLARE @description187 AS sql_variant;
SET @description187 = N'識別主鍵';
EXEC sp_addextendedproperty 'MS_Description', @description187, 'SCHEMA', N'core', 'TABLE', N'SystemSettingChangeLogs', 'COLUMN', N'Id';

DECLARE @description188 AS sql_variant;
SET @description188 = N'用戶識別碼';
EXEC sp_addextendedproperty 'MS_Description', @description188, 'SCHEMA', N'core', 'TABLE', N'SystemLogs', 'COLUMN', N'UserId';

DECLARE @description189 AS sql_variant;
SET @description189 = N'URL';
EXEC sp_addextendedproperty 'MS_Description', @description189, 'SCHEMA', N'core', 'TABLE', N'SystemLogs', 'COLUMN', N'Url';

DECLARE @description190 AS sql_variant;
SET @description190 = N'時間戳記';
EXEC sp_addextendedproperty 'MS_Description', @description190, 'SCHEMA', N'core', 'TABLE', N'SystemLogs', 'COLUMN', N'TimeStamp';

DECLARE @description191 AS sql_variant;
SET @description191 = N'區域實驗室關聯識別碼';
EXEC sp_addextendedproperty 'MS_Description', @description191, 'SCHEMA', N'core', 'TABLE', N'SystemLogs', 'COLUMN', N'TenantId';

DECLARE @description192 AS sql_variant;
SET @description192 = N'前一筆雜湊值';
EXEC sp_addextendedproperty 'MS_Description', @description192, 'SCHEMA', N'core', 'TABLE', N'SystemLogs', 'COLUMN', N'PrevHash';

DECLARE @description193 AS sql_variant;
SET @description193 = N'有效載荷';
EXEC sp_addextendedproperty 'MS_Description', @description193, 'SCHEMA', N'core', 'TABLE', N'SystemLogs', 'COLUMN', N'Payload';

DECLARE @description194 AS sql_variant;
SET @description194 = N'操作';
EXEC sp_addextendedproperty 'MS_Description', @description194, 'SCHEMA', N'core', 'TABLE', N'SystemLogs', 'COLUMN', N'Operation';

DECLARE @description195 AS sql_variant;
SET @description195 = N'模組名稱';
EXEC sp_addextendedproperty 'MS_Description', @description195, 'SCHEMA', N'core', 'TABLE', N'SystemLogs', 'COLUMN', N'ModuleName';

DECLARE @description196 AS sql_variant;
SET @description196 = N'訊息';
EXEC sp_addextendedproperty 'MS_Description', @description196, 'SCHEMA', N'core', 'TABLE', N'SystemLogs', 'COLUMN', N'Message';

DECLARE @description197 AS sql_variant;
SET @description197 = N'層級';
EXEC sp_addextendedproperty 'MS_Description', @description197, 'SCHEMA', N'core', 'TABLE', N'SystemLogs', 'COLUMN', N'Level';

DECLARE @description198 AS sql_variant;
SET @description198 = N'IP位址';
EXEC sp_addextendedproperty 'MS_Description', @description198, 'SCHEMA', N'core', 'TABLE', N'SystemLogs', 'COLUMN', N'IP';

DECLARE @description199 AS sql_variant;
SET @description199 = N'特徵碼';
EXEC sp_addextendedproperty 'MS_Description', @description199, 'SCHEMA', N'core', 'TABLE', N'SystemLogs', 'COLUMN', N'Fingerprint';

DECLARE @description200 AS sql_variant;
SET @description200 = N'異常';
EXEC sp_addextendedproperty 'MS_Description', @description200, 'SCHEMA', N'core', 'TABLE', N'SystemLogs', 'COLUMN', N'Exception';

DECLARE @description201 AS sql_variant;
SET @description201 = N'雜湊值';
EXEC sp_addextendedproperty 'MS_Description', @description201, 'SCHEMA', N'core', 'TABLE', N'SystemLogs', 'COLUMN', N'CurrentHash';

DECLARE @description202 AS sql_variant;
SET @description202 = N'建立時間';
EXEC sp_addextendedproperty 'MS_Description', @description202, 'SCHEMA', N'core', 'TABLE', N'SystemLogs', 'COLUMN', N'CreatedAt';

DECLARE @description203 AS sql_variant;
SET @description203 = N'關聯識別碼';
EXEC sp_addextendedproperty 'MS_Description', @description203, 'SCHEMA', N'core', 'TABLE', N'SystemLogs', 'COLUMN', N'CorrelationId';

DECLARE @description204 AS sql_variant;
SET @description204 = N'告警識別碼';
EXEC sp_addextendedproperty 'MS_Description', @description204, 'SCHEMA', N'core', 'TABLE', N'SystemLogs', 'COLUMN', N'AlertId';

DECLARE @description205 AS sql_variant;
SET @description205 = N'識別鍵';
EXEC sp_addextendedproperty 'MS_Description', @description205, 'SCHEMA', N'core', 'TABLE', N'SystemLogs', 'COLUMN', N'Id';

DECLARE @description206 AS sql_variant;
SET @description206 = N'觸發事件的用戶識別碼';
EXEC sp_addextendedproperty 'MS_Description', @description206, 'SCHEMA', N'core', 'TABLE', N'SecurityLog', 'COLUMN', N'UserId';

DECLARE @description207 AS sql_variant;
SET @description207 = N'高精度時間戳記 (帶時區)';
EXEC sp_addextendedproperty 'MS_Description', @description207, 'SCHEMA', N'core', 'TABLE', N'SecurityLog', 'COLUMN', N'Timestamp';

DECLARE @description208 AS sql_variant;
SET @description208 = N'隨附結構化快照內容 (JSON 格式)';
EXEC sp_addextendedproperty 'MS_Description', @description208, 'SCHEMA', N'core', 'TABLE', N'SecurityLog', 'COLUMN', N'Properties';

DECLARE @description209 AS sql_variant;
SET @description209 = N'日誌訊息';
EXEC sp_addextendedproperty 'MS_Description', @description209, 'SCHEMA', N'core', 'TABLE', N'SecurityLog', 'COLUMN', N'Message';

DECLARE @description210 AS sql_variant;
SET @description210 = N'日誌分流類型 (例如: Security)';
EXEC sp_addextendedproperty 'MS_Description', @description210, 'SCHEMA', N'core', 'TABLE', N'SecurityLog', 'COLUMN', N'LogType';

DECLARE @description211 AS sql_variant;
SET @description211 = N'日誌層級 (例如: Information, Warning, Error)';
EXEC sp_addextendedproperty 'MS_Description', @description211, 'SCHEMA', N'core', 'TABLE', N'SecurityLog', 'COLUMN', N'Level';

DECLARE @description212 AS sql_variant;
SET @description212 = N'特徵碼';
EXEC sp_addextendedproperty 'MS_Description', @description212, 'SCHEMA', N'core', 'TABLE', N'SecurityLog', 'COLUMN', N'Fingerprint';

DECLARE @description213 AS sql_variant;
SET @description213 = N'異常堆疊追蹤資訊';
EXEC sp_addextendedproperty 'MS_Description', @description213, 'SCHEMA', N'core', 'TABLE', N'SecurityLog', 'COLUMN', N'Exception';

DECLARE @description214 AS sql_variant;
SET @description214 = N'稽核事件分類 (例如: Auth.TokenRefresh)';
EXEC sp_addextendedproperty 'MS_Description', @description214, 'SCHEMA', N'core', 'TABLE', N'SecurityLog', 'COLUMN', N'EventCategory';

DECLARE @description215 AS sql_variant;
SET @description215 = N'關聯識別碼';
EXEC sp_addextendedproperty 'MS_Description', @description215, 'SCHEMA', N'core', 'TABLE', N'SecurityLog', 'COLUMN', N'CorrelationId';

DECLARE @description216 AS sql_variant;
SET @description216 = N'客戶端來源 IP 位址';
EXEC sp_addextendedproperty 'MS_Description', @description216, 'SCHEMA', N'core', 'TABLE', N'SecurityLog', 'COLUMN', N'ClientIp';

DECLARE @description217 AS sql_variant;
SET @description217 = N'告警識別碼';
EXEC sp_addextendedproperty 'MS_Description', @description217, 'SCHEMA', N'core', 'TABLE', N'SecurityLog', 'COLUMN', N'AlertId';

DECLARE @description218 AS sql_variant;
SET @description218 = N'流水號主鍵';
EXEC sp_addextendedproperty 'MS_Description', @description218, 'SCHEMA', N'core', 'TABLE', N'SecurityLog', 'COLUMN', N'Id';

DECLARE @description219 AS sql_variant;
SET @description219 = N'區域實驗室的 TenantLabId（ORG Organization.TenantLabId）';
EXEC sp_addextendedproperty 'MS_Description', @description219, 'SCHEMA', N'core', 'TABLE', N'RoleLabScopes', 'COLUMN', N'TenantLabId';

DECLARE @description220 AS sql_variant;
SET @description220 = N'角色識別鍵';
EXEC sp_addextendedproperty 'MS_Description', @description220, 'SCHEMA', N'core', 'TABLE', N'RoleLabScopes', 'COLUMN', N'RoleId';

DECLARE @description221 AS sql_variant;
SET @description221 = N'核准後要套用的完整權限鍵清單（JSON 陣列）';
EXEC sp_addextendedproperty 'MS_Description', @description221, 'SCHEMA', N'core', 'TABLE', N'RoleChangeRequests', 'COLUMN', N'TargetPermissionKeys';

DECLARE @description222 AS sql_variant;
SET @description222 = N'申請狀態';
EXEC sp_addextendedproperty 'MS_Description', @description222, 'SCHEMA', N'core', 'TABLE', N'RoleChangeRequests', 'COLUMN', N'Status';

DECLARE @description223 AS sql_variant;
SET @description223 = N'角色名稱';
EXEC sp_addextendedproperty 'MS_Description', @description223, 'SCHEMA', N'core', 'TABLE', N'RoleChangeRequests', 'COLUMN', N'RoleName';

DECLARE @description224 AS sql_variant;
SET @description224 = N'角色識別鍵';
EXEC sp_addextendedproperty 'MS_Description', @description224, 'SCHEMA', N'core', 'TABLE', N'RoleChangeRequests', 'COLUMN', N'RoleId';

DECLARE @description225 AS sql_variant;
SET @description225 = N'審核者名稱';
EXEC sp_addextendedproperty 'MS_Description', @description225, 'SCHEMA', N'core', 'TABLE', N'RoleChangeRequests', 'COLUMN', N'ReviewedByName';

DECLARE @description226 AS sql_variant;
SET @description226 = N'審核者識別鍵';
EXEC sp_addextendedproperty 'MS_Description', @description226, 'SCHEMA', N'core', 'TABLE', N'RoleChangeRequests', 'COLUMN', N'ReviewedBy';

DECLARE @description227 AS sql_variant;
SET @description227 = N'審核時間';
EXEC sp_addextendedproperty 'MS_Description', @description227, 'SCHEMA', N'core', 'TABLE', N'RoleChangeRequests', 'COLUMN', N'ReviewedAt';

DECLARE @description228 AS sql_variant;
SET @description228 = N'審核意見';
EXEC sp_addextendedproperty 'MS_Description', @description228, 'SCHEMA', N'core', 'TABLE', N'RoleChangeRequests', 'COLUMN', N'ReviewComment';

DECLARE @description229 AS sql_variant;
SET @description229 = N'申請者名稱';
EXEC sp_addextendedproperty 'MS_Description', @description229, 'SCHEMA', N'core', 'TABLE', N'RoleChangeRequests', 'COLUMN', N'RequestedByName';

DECLARE @description230 AS sql_variant;
SET @description230 = N'申請者識別鍵';
EXEC sp_addextendedproperty 'MS_Description', @description230, 'SCHEMA', N'core', 'TABLE', N'RoleChangeRequests', 'COLUMN', N'RequestedBy';

DECLARE @description231 AS sql_variant;
SET @description231 = N'申請時間';
EXEC sp_addextendedproperty 'MS_Description', @description231, 'SCHEMA', N'core', 'TABLE', N'RoleChangeRequests', 'COLUMN', N'RequestedAt';

DECLARE @description232 AS sql_variant;
SET @description232 = N'提出者說明（變更原因）';
EXEC sp_addextendedproperty 'MS_Description', @description232, 'SCHEMA', N'core', 'TABLE', N'RoleChangeRequests', 'COLUMN', N'RequestReason';

DECLARE @description233 AS sql_variant;
SET @description233 = N'相對於送出當下的移除權限（JSON 陣列）';
EXEC sp_addextendedproperty 'MS_Description', @description233, 'SCHEMA', N'core', 'TABLE', N'RoleChangeRequests', 'COLUMN', N'RemovedPermissionKeys';

DECLARE @description234 AS sql_variant;
SET @description234 = N'送出申請當下的角色狀態';
EXEC sp_addextendedproperty 'MS_Description', @description234, 'SCHEMA', N'core', 'TABLE', N'RoleChangeRequests', 'COLUMN', N'PreviousRoleStatus';

DECLARE @description235 AS sql_variant;
SET @description235 = N'送出當下的角色成員數（影響範圍）';
EXEC sp_addextendedproperty 'MS_Description', @description235, 'SCHEMA', N'core', 'TABLE', N'RoleChangeRequests', 'COLUMN', N'AffectedUserCount';

DECLARE @description236 AS sql_variant;
SET @description236 = N'相對於送出當下的新增權限（JSON 陣列）';
EXEC sp_addextendedproperty 'MS_Description', @description236, 'SCHEMA', N'core', 'TABLE', N'RoleChangeRequests', 'COLUMN', N'AddedPermissionKeys';

DECLARE @description237 AS sql_variant;
SET @description237 = N'識別主鍵';
EXEC sp_addextendedproperty 'MS_Description', @description237, 'SCHEMA', N'core', 'TABLE', N'RoleChangeRequests', 'COLUMN', N'Id';

DECLARE @description238 AS sql_variant;
SET @description238 = N'人可讀的異動摘要';
EXEC sp_addextendedproperty 'MS_Description', @description238, 'SCHEMA', N'core', 'TABLE', N'RoleAuditLogs', 'COLUMN', N'Summary';

DECLARE @description239 AS sql_variant;
SET @description239 = N'異動當下的角色名稱（角色刪除或更名後仍可辨識）';
EXEC sp_addextendedproperty 'MS_Description', @description239, 'SCHEMA', N'core', 'TABLE', N'RoleAuditLogs', 'COLUMN', N'RoleName';

DECLARE @description240 AS sql_variant;
SET @description240 = N'角色識別鍵';
EXEC sp_addextendedproperty 'MS_Description', @description240, 'SCHEMA', N'core', 'TABLE', N'RoleAuditLogs', 'COLUMN', N'RoleId';

DECLARE @description241 AS sql_variant;
SET @description241 = N'補充明細（JSON，例如權限差異、成員清單）';
EXEC sp_addextendedproperty 'MS_Description', @description241, 'SCHEMA', N'core', 'TABLE', N'RoleAuditLogs', 'COLUMN', N'Detail';

DECLARE @description242 AS sql_variant;
SET @description242 = N'建立時間';
EXEC sp_addextendedproperty 'MS_Description', @description242, 'SCHEMA', N'core', 'TABLE', N'RoleAuditLogs', 'COLUMN', N'CreatedAt';

DECLARE @description243 AS sql_variant;
SET @description243 = N'受影響的使用者人數';
EXEC sp_addextendedproperty 'MS_Description', @description243, 'SCHEMA', N'core', 'TABLE', N'RoleAuditLogs', 'COLUMN', N'AffectedUserCount';

DECLARE @description244 AS sql_variant;
SET @description244 = N'操作者用戶識別鍵';
EXEC sp_addextendedproperty 'MS_Description', @description244, 'SCHEMA', N'core', 'TABLE', N'RoleAuditLogs', 'COLUMN', N'ActorUserId';

DECLARE @description245 AS sql_variant;
SET @description245 = N'操作者名稱';
EXEC sp_addextendedproperty 'MS_Description', @description245, 'SCHEMA', N'core', 'TABLE', N'RoleAuditLogs', 'COLUMN', N'ActorName';

DECLARE @description246 AS sql_variant;
SET @description246 = N'動作代碼，見 RoleAuditActions';
EXEC sp_addextendedproperty 'MS_Description', @description246, 'SCHEMA', N'core', 'TABLE', N'RoleAuditLogs', 'COLUMN', N'Action';

DECLARE @description247 AS sql_variant;
SET @description247 = N'識別主鍵';
EXEC sp_addextendedproperty 'MS_Description', @description247, 'SCHEMA', N'core', 'TABLE', N'RoleAuditLogs', 'COLUMN', N'Id';

DECLARE @description248 AS sql_variant;
SET @description248 = N'最後修改者';
EXEC sp_addextendedproperty 'MS_Description', @description248, 'SCHEMA', N'core', 'TABLE', N'Role_Global_Permissions', 'COLUMN', N'updated_by';

DECLARE @description249 AS sql_variant;
SET @description249 = N'最後修改時間 (UTC)';
EXEC sp_addextendedproperty 'MS_Description', @description249, 'SCHEMA', N'core', 'TABLE', N'Role_Global_Permissions', 'COLUMN', N'updated_at_utc';

DECLARE @description250 AS sql_variant;
SET @description250 = N'最後修改時間';
EXEC sp_addextendedproperty 'MS_Description', @description250, 'SCHEMA', N'core', 'TABLE', N'Role_Global_Permissions', 'COLUMN', N'updated_at';

DECLARE @description251 AS sql_variant;
SET @description251 = N'角色識別碼 (對應 ASP.NET Core Identity RoleId)';
EXEC sp_addextendedproperty 'MS_Description', @description251, 'SCHEMA', N'core', 'TABLE', N'Role_Global_Permissions', 'COLUMN', N'role_id';

DECLARE @description252 AS sql_variant;
SET @description252 = N'全域權限或模組識別 Key';
EXEC sp_addextendedproperty 'MS_Description', @description252, 'SCHEMA', N'core', 'TABLE', N'Role_Global_Permissions', 'COLUMN', N'permission_key';

DECLARE @description253 AS sql_variant;
SET @description253 = N'建立者';
EXEC sp_addextendedproperty 'MS_Description', @description253, 'SCHEMA', N'core', 'TABLE', N'Role_Global_Permissions', 'COLUMN', N'created_by';

DECLARE @description254 AS sql_variant;
SET @description254 = N'建立時間 (UTC)';
EXEC sp_addextendedproperty 'MS_Description', @description254, 'SCHEMA', N'core', 'TABLE', N'Role_Global_Permissions', 'COLUMN', N'created_at_utc';

DECLARE @description255 AS sql_variant;
SET @description255 = N'建立時間';
EXEC sp_addextendedproperty 'MS_Description', @description255, 'SCHEMA', N'core', 'TABLE', N'Role_Global_Permissions', 'COLUMN', N'created_at';

DECLARE @description256 AS sql_variant;
SET @description256 = N'64 位元權限遮罩值 (Bitmask 0-63)';
EXEC sp_addextendedproperty 'MS_Description', @description256, 'SCHEMA', N'core', 'TABLE', N'Role_Global_Permissions', 'COLUMN', N'bitmask';

DECLARE @description257 AS sql_variant;
SET @description257 = N'唯一識別碼 (Primary Key)';
EXEC sp_addextendedproperty 'MS_Description', @description257, 'SCHEMA', N'core', 'TABLE', N'Role_Global_Permissions', 'COLUMN', N'id';

DECLARE @description258 AS sql_variant;
SET @description258 = N'關聯的用戶識別碼';
EXEC sp_addextendedproperty 'MS_Description', @description258, 'SCHEMA', N'core', 'TABLE', N'RemediationTickets', 'COLUMN', N'UserId';

DECLARE @description259 AS sql_variant;
SET @description259 = N'核發此憑證時的資安風險原因說明';
EXEC sp_addextendedproperty 'MS_Description', @description259, 'SCHEMA', N'core', 'TABLE', N'RemediationTickets', 'COLUMN', N'RemediationReason';

DECLARE @description260 AS sql_variant;
SET @description260 = N'憑證核發時間';
EXEC sp_addextendedproperty 'MS_Description', @description260, 'SCHEMA', N'core', 'TABLE', N'RemediationTickets', 'COLUMN', N'IssuedAt';

DECLARE @description261 AS sql_variant;
SET @description261 = N'該憑證是否已被使用：關鍵管控，防止 Replay Attack（重放攻擊）';
EXEC sp_addextendedproperty 'MS_Description', @description261, 'SCHEMA', N'core', 'TABLE', N'RemediationTickets', 'COLUMN', N'IsUsed';

DECLARE @description262 AS sql_variant;
SET @description262 = N'憑證失效截止時間：關鍵管控，時效性驗證，逾期即作廢';
EXEC sp_addextendedproperty 'MS_Description', @description262, 'SCHEMA', N'core', 'TABLE', N'RemediationTickets', 'COLUMN', N'ExpiresAt';

DECLARE @description263 AS sql_variant;
SET @description263 = N'核發此憑證時的客戶端來源 IP (供後續日誌鑑識比對)';
EXEC sp_addextendedproperty 'MS_Description', @description263, 'SCHEMA', N'core', 'TABLE', N'RemediationTickets', 'COLUMN', N'CreatedByIp';

DECLARE @description264 AS sql_variant;
SET @description264 = N'憑證唯一識別碼 (通常為安全隨機生成的 GUID 或加密記號)';
EXEC sp_addextendedproperty 'MS_Description', @description264, 'SCHEMA', N'core', 'TABLE', N'RemediationTickets', 'COLUMN', N'TicketId';

DECLARE @description265 AS sql_variant;
SET @description265 = N'角色識別鍵';
EXEC sp_addextendedproperty 'MS_Description', @description265, 'SCHEMA', N'core', 'TABLE', N'PermissionGrants', 'COLUMN', N'RoleId';

DECLARE @description266 AS sql_variant;
SET @description266 = N'動態 BitMask 權限位元組陣列 (預設 64 bytes = 512 個權限點)';
EXEC sp_addextendedproperty 'MS_Description', @description266, 'SCHEMA', N'core', 'TABLE', N'PermissionGrants', 'COLUMN', N'PermissionVector';

DECLARE @description267 AS sql_variant;
SET @description267 = N'跨實驗室隔離維度 ID';
EXEC sp_addextendedproperty 'MS_Description', @description267, 'SCHEMA', N'core', 'TABLE', N'PermissionGrants', 'COLUMN', N'LabId';

DECLARE @description268 AS sql_variant;
SET @description268 = N'識別主鍵';
EXEC sp_addextendedproperty 'MS_Description', @description268, 'SCHEMA', N'core', 'TABLE', N'PermissionGrants', 'COLUMN', N'Id';

DECLARE @description269 AS sql_variant;
SET @description269 = N'版本';
EXEC sp_addextendedproperty 'MS_Description', @description269, 'SCHEMA', N'core', 'TABLE', N'ModuleMetadatas', 'COLUMN', N'Version';

DECLARE @description270 AS sql_variant;
SET @description270 = N'模組標題';
EXEC sp_addextendedproperty 'MS_Description', @description270, 'SCHEMA', N'core', 'TABLE', N'ModuleMetadatas', 'COLUMN', N'ModuleTitle';

DECLARE @description271 AS sql_variant;
SET @description271 = N'模組名稱';
EXEC sp_addextendedproperty 'MS_Description', @description271, 'SCHEMA', N'core', 'TABLE', N'ModuleMetadatas', 'COLUMN', N'ModuleName';

DECLARE @description272 AS sql_variant;
SET @description272 = N'最後載入時間';
EXEC sp_addextendedproperty 'MS_Description', @description272, 'SCHEMA', N'core', 'TABLE', N'ModuleMetadatas', 'COLUMN', N'LastLoadedAt';

DECLARE @description273 AS sql_variant;
SET @description273 = N'是否啟用';
EXEC sp_addextendedproperty 'MS_Description', @description273, 'SCHEMA', N'core', 'TABLE', N'ModuleMetadatas', 'COLUMN', N'IsActive';

DECLARE @description274 AS sql_variant;
SET @description274 = N'校驗和';
EXEC sp_addextendedproperty 'MS_Description', @description274, 'SCHEMA', N'core', 'TABLE', N'ModuleMetadatas', 'COLUMN', N'Checksum';

DECLARE @description275 AS sql_variant;
SET @description275 = N'組件路徑';
EXEC sp_addextendedproperty 'MS_Description', @description275, 'SCHEMA', N'core', 'TABLE', N'ModuleMetadatas', 'COLUMN', N'AssemblyPath';

DECLARE @description276 AS sql_variant;
SET @description276 = N'識別主鍵';
EXEC sp_addextendedproperty 'MS_Description', @description276, 'SCHEMA', N'core', 'TABLE', N'ModuleMetadatas', 'COLUMN', N'Id';

DECLARE @description277 AS sql_variant;
SET @description277 = N'節點類型 (Section, Group, Page)';
EXEC sp_addextendedproperty 'MS_Description', @description277, 'SCHEMA', N'core', 'TABLE', N'MenuItems', 'COLUMN', N'Type';

DECLARE @description278 AS sql_variant;
SET @description278 = N'前端路由路徑 (僅 MenuType.Page 必須填寫)';
EXEC sp_addextendedproperty 'MS_Description', @description278, 'SCHEMA', N'core', 'TABLE', N'MenuItems', 'COLUMN', N'Route';

DECLARE @description279 AS sql_variant;
SET @description279 = N'關聯的權限鍵值';
EXEC sp_addextendedproperty 'MS_Description', @description279, 'SCHEMA', N'core', 'TABLE', N'MenuItems', 'COLUMN', N'PermissionKey';

DECLARE @description280 AS sql_variant;
SET @description280 = N'父節點識別鍵';
EXEC sp_addextendedproperty 'MS_Description', @description280, 'SCHEMA', N'core', 'TABLE', N'MenuItems', 'COLUMN', N'ParentId';

DECLARE @description281 AS sql_variant;
SET @description281 = N'模組識別碼 (區分主專案與 SES 外掛模組，如: "System", "ORG")';
EXEC sp_addextendedproperty 'MS_Description', @description281, 'SCHEMA', N'core', 'TABLE', N'MenuItems', 'COLUMN', N'ModuleName';

DECLARE @description282 AS sql_variant;
SET @description282 = N'節點識別 Key (例如: ORG.LABORATORY.LIST)';
EXEC sp_addextendedproperty 'MS_Description', @description282, 'SCHEMA', N'core', 'TABLE', N'MenuItems', 'COLUMN', N'Key';

DECLARE @description283 AS sql_variant;
SET @description283 = N'是否在選單中顯示 (預留隱藏路由支援)';
EXEC sp_addextendedproperty 'MS_Description', @description283, 'SCHEMA', N'core', 'TABLE', N'MenuItems', 'COLUMN', N'IsVisible';

DECLARE @description284 AS sql_variant;
SET @description284 = N'是否啟用';
EXEC sp_addextendedproperty 'MS_Description', @description284, 'SCHEMA', N'core', 'TABLE', N'MenuItems', 'COLUMN', N'IsActive';

DECLARE @description285 AS sql_variant;
SET @description285 = N'圖示';
EXEC sp_addextendedproperty 'MS_Description', @description285, 'SCHEMA', N'core', 'TABLE', N'MenuItems', 'COLUMN', N'Icon';

DECLARE @description286 AS sql_variant;
SET @description286 = N'顯示排序';
EXEC sp_addextendedproperty 'MS_Description', @description286, 'SCHEMA', N'core', 'TABLE', N'MenuItems', 'COLUMN', N'DisplayOrder';

DECLARE @description287 AS sql_variant;
SET @description287 = N'顯示名稱';
EXEC sp_addextendedproperty 'MS_Description', @description287, 'SCHEMA', N'core', 'TABLE', N'MenuItems', 'COLUMN', N'DisplayName';

DECLARE @description288 AS sql_variant;
SET @description288 = N'控制器識別鍵';
EXEC sp_addextendedproperty 'MS_Description', @description288, 'SCHEMA', N'core', 'TABLE', N'MenuItems', 'COLUMN', N'ControllerId';

DECLARE @description289 AS sql_variant;
SET @description289 = N'前端對應 Component 組件路徑 (適用於 Vue/Blazor 動態載入)';
EXEC sp_addextendedproperty 'MS_Description', @description289, 'SCHEMA', N'core', 'TABLE', N'MenuItems', 'COLUMN', N'Component';

DECLARE @description290 AS sql_variant;
SET @description290 = N'識別主鍵';
EXEC sp_addextendedproperty 'MS_Description', @description290, 'SCHEMA', N'core', 'TABLE', N'MenuItems', 'COLUMN', N'Id';

DECLARE @description291 AS sql_variant;
SET @description291 = N'最後更新人員名稱';
EXEC sp_addextendedproperty 'MS_Description', @description291, 'SCHEMA', N'core', 'TABLE', N'LabManagers', 'COLUMN', N'UpdatedByName';

DECLARE @description292 AS sql_variant;
SET @description292 = N'最後更新時間';
EXEC sp_addextendedproperty 'MS_Description', @description292, 'SCHEMA', N'core', 'TABLE', N'LabManagers', 'COLUMN', N'UpdatedAt';

DECLARE @description293 AS sql_variant;
SET @description293 = N'主管使用者識別鍵';
EXEC sp_addextendedproperty 'MS_Description', @description293, 'SCHEMA', N'core', 'TABLE', N'LabManagers', 'COLUMN', N'ManagerUserId';

DECLARE @description294 AS sql_variant;
SET @description294 = N'區域實驗室識別鍵';
EXEC sp_addextendedproperty 'MS_Description', @description294, 'SCHEMA', N'core', 'TABLE', N'LabManagers', 'COLUMN', N'TenantLabId';

DECLARE @description295 AS sql_variant;
SET @description295 = N'權限鍵';
EXEC sp_addextendedproperty 'MS_Description', @description295, 'SCHEMA', N'core', 'TABLE', N'LabFunctionScopes', 'COLUMN', N'PermissionKey';

DECLARE @description296 AS sql_variant;
SET @description296 = N'區域實驗室識別碼（ORG Organization.TenantLabId）';
EXEC sp_addextendedproperty 'MS_Description', @description296, 'SCHEMA', N'core', 'TABLE', N'LabFunctionScopes', 'COLUMN', N'TenantLabId';

DECLARE @description297 AS sql_variant;
SET @description297 = N'最後更新人員名稱';
EXEC sp_addextendedproperty 'MS_Description', @description297, 'SCHEMA', N'core', 'TABLE', N'LabFunctionPolicies', 'COLUMN', N'UpdatedByName';

DECLARE @description298 AS sql_variant;
SET @description298 = N'最後更新人員識別鍵';
EXEC sp_addextendedproperty 'MS_Description', @description298, 'SCHEMA', N'core', 'TABLE', N'LabFunctionPolicies', 'COLUMN', N'UpdatedBy';

DECLARE @description299 AS sql_variant;
SET @description299 = N'最後更新時間';
EXEC sp_addextendedproperty 'MS_Description', @description299, 'SCHEMA', N'core', 'TABLE', N'LabFunctionPolicies', 'COLUMN', N'UpdatedAt';

DECLARE @description300 AS sql_variant;
SET @description300 = N'是否限制功能';
EXEC sp_addextendedproperty 'MS_Description', @description300, 'SCHEMA', N'core', 'TABLE', N'LabFunctionPolicies', 'COLUMN', N'IsRestricted';

DECLARE @description301 AS sql_variant;
SET @description301 = N'區域實驗室識別碼（ORG Organization.TenantLabId）';
EXEC sp_addextendedproperty 'MS_Description', @description301, 'SCHEMA', N'core', 'TABLE', N'LabFunctionPolicies', 'COLUMN', N'TenantLabId';

DECLARE @description302 AS sql_variant;
SET @description302 = N'使用者識別鍵';
EXEC sp_addextendedproperty 'MS_Description', @description302, 'SCHEMA', N'core', 'TABLE', N'LabAuditAssignments', 'COLUMN', N'UserId';

DECLARE @description303 AS sql_variant;
SET @description303 = N'受稽核的區域實驗室（ORG Organization.TenantLabId）';
EXEC sp_addextendedproperty 'MS_Description', @description303, 'SCHEMA', N'core', 'TABLE', N'LabAuditAssignments', 'COLUMN', N'TenantLabId';

DECLARE @description304 AS sql_variant;
SET @description304 = N'撤銷人員識別鍵';
EXEC sp_addextendedproperty 'MS_Description', @description304, 'SCHEMA', N'core', 'TABLE', N'LabAuditAssignments', 'COLUMN', N'RevokedByName';

DECLARE @description305 AS sql_variant;
SET @description305 = N'撤銷時間';
EXEC sp_addextendedproperty 'MS_Description', @description305, 'SCHEMA', N'core', 'TABLE', N'LabAuditAssignments', 'COLUMN', N'RevokedAt';

DECLARE @description306 AS sql_variant;
SET @description306 = N'撤銷原因';
EXEC sp_addextendedproperty 'MS_Description', @description306, 'SCHEMA', N'core', 'TABLE', N'LabAuditAssignments', 'COLUMN', N'RevokeReason';

DECLARE @description307 AS sql_variant;
SET @description307 = N'稽核原因 / 稽核案號';
EXEC sp_addextendedproperty 'MS_Description', @description307, 'SCHEMA', N'core', 'TABLE', N'LabAuditAssignments', 'COLUMN', N'Reason';

DECLARE @description308 AS sql_variant;
SET @description308 = N'生效迄止（含）';
EXEC sp_addextendedproperty 'MS_Description', @description308, 'SCHEMA', N'core', 'TABLE', N'LabAuditAssignments', 'COLUMN', N'EffectiveTo';

DECLARE @description309 AS sql_variant;
SET @description309 = N'生效起始（含）';
EXEC sp_addextendedproperty 'MS_Description', @description309, 'SCHEMA', N'core', 'TABLE', N'LabAuditAssignments', 'COLUMN', N'EffectiveFrom';

DECLARE @description310 AS sql_variant;
SET @description310 = N'指派時間';
EXEC sp_addextendedproperty 'MS_Description', @description310, 'SCHEMA', N'core', 'TABLE', N'LabAuditAssignments', 'COLUMN', N'CreatedAt';

DECLARE @description311 AS sql_variant;
SET @description311 = N'指派人員名稱';
EXEC sp_addextendedproperty 'MS_Description', @description311, 'SCHEMA', N'core', 'TABLE', N'LabAuditAssignments', 'COLUMN', N'AssignedByName';

DECLARE @description312 AS sql_variant;
SET @description312 = N'指派人員識別鍵';
EXEC sp_addextendedproperty 'MS_Description', @description312, 'SCHEMA', N'core', 'TABLE', N'LabAuditAssignments', 'COLUMN', N'AssignedBy';

DECLARE @description313 AS sql_variant;
SET @description313 = N'識別主鍵';
EXEC sp_addextendedproperty 'MS_Description', @description313, 'SCHEMA', N'core', 'TABLE', N'LabAuditAssignments', 'COLUMN', N'Id';

DECLARE @description314 AS sql_variant;
SET @description314 = N'內部變更原因（僅管理者與稽核可見）';
EXEC sp_addextendedproperty 'MS_Description', @description314, 'SCHEMA', N'core', 'TABLE', N'FunctionStatusLogs', 'COLUMN', N'Reason';

DECLARE @description315 AS sql_variant;
SET @description315 = N'權限金鑰';
EXEC sp_addextendedproperty 'MS_Description', @description315, 'SCHEMA', N'core', 'TABLE', N'FunctionStatusLogs', 'COLUMN', N'PermissionKey';

DECLARE @description316 AS sql_variant;
SET @description316 = N'對使用者公告內容（停用時顯示於系統公告與 API 拒絕訊息）';
EXEC sp_addextendedproperty 'MS_Description', @description316, 'SCHEMA', N'core', 'TABLE', N'FunctionStatusLogs', 'COLUMN', N'Notice';

DECLARE @description317 AS sql_variant;
SET @description317 = N'異動後狀態：true = 啟用、false = 停用';
EXEC sp_addextendedproperty 'MS_Description', @description317, 'SCHEMA', N'core', 'TABLE', N'FunctionStatusLogs', 'COLUMN', N'IsActive';

DECLARE @description318 AS sql_variant;
SET @description318 = N'預計恢復時間（僅供公告，不會自動恢復）';
EXEC sp_addextendedproperty 'MS_Description', @description318, 'SCHEMA', N'core', 'TABLE', N'FunctionStatusLogs', 'COLUMN', N'ExpectedRestoreAt';

DECLARE @description319 AS sql_variant;
SET @description319 = N'功能顯示名稱';
EXEC sp_addextendedproperty 'MS_Description', @description319, 'SCHEMA', N'core', 'TABLE', N'FunctionStatusLogs', 'COLUMN', N'DisplayName';

DECLARE @description320 AS sql_variant;
SET @description320 = N'建立時間';
EXEC sp_addextendedproperty 'MS_Description', @description320, 'SCHEMA', N'core', 'TABLE', N'FunctionStatusLogs', 'COLUMN', N'CreatedAt';

DECLARE @description321 AS sql_variant;
SET @description321 = N'異動當下的功能識別（中繼資料重新掃描或移除後仍可辨識）';
EXEC sp_addextendedproperty 'MS_Description', @description321, 'SCHEMA', N'core', 'TABLE', N'FunctionStatusLogs', 'COLUMN', N'ControllerName';

DECLARE @description322 AS sql_variant;
SET @description322 = N'控制器中繼資料識別';
EXEC sp_addextendedproperty 'MS_Description', @description322, 'SCHEMA', N'core', 'TABLE', N'FunctionStatusLogs', 'COLUMN', N'ControllerMetadataId';

DECLARE @description323 AS sql_variant;
SET @description323 = N'來源 IP';
EXEC sp_addextendedproperty 'MS_Description', @description323, 'SCHEMA', N'core', 'TABLE', N'FunctionStatusLogs', 'COLUMN', N'ClientIp';

DECLARE @description324 AS sql_variant;
SET @description324 = N'操作者識別';
EXEC sp_addextendedproperty 'MS_Description', @description324, 'SCHEMA', N'core', 'TABLE', N'FunctionStatusLogs', 'COLUMN', N'ActorUserId';

DECLARE @description325 AS sql_variant;
SET @description325 = N'操作者名稱';
EXEC sp_addextendedproperty 'MS_Description', @description325, 'SCHEMA', N'core', 'TABLE', N'FunctionStatusLogs', 'COLUMN', N'ActorName';

DECLARE @description326 AS sql_variant;
SET @description326 = N'動作名稱';
EXEC sp_addextendedproperty 'MS_Description', @description326, 'SCHEMA', N'core', 'TABLE', N'FunctionStatusLogs', 'COLUMN', N'ActionName';

DECLARE @description327 AS sql_variant;
SET @description327 = N'識別主鍵';
EXEC sp_addextendedproperty 'MS_Description', @description327, 'SCHEMA', N'core', 'TABLE', N'FunctionStatusLogs', 'COLUMN', N'Id';

DECLARE @description328 AS sql_variant;
SET @description328 = N'識別主鍵';
EXEC sp_addextendedproperty 'MS_Description', @description328, 'SCHEMA', N'core', 'TABLE', N'ControllerMetadatas', 'COLUMN', N'Id';

DECLARE @description329 AS sql_variant;
SET @description329 = N'使用者識別';
EXEC sp_addextendedproperty 'MS_Description', @description329, 'SCHEMA', N'core', 'TABLE', N'AuditLogs', 'COLUMN', N'UserId';

DECLARE @description330 AS sql_variant;
SET @description330 = N'全鏈路追蹤 ID (對應 W3C TraceId 格式 32 位元 HEX)';
EXEC sp_addextendedproperty 'MS_Description', @description330, 'SCHEMA', N'core', 'TABLE', N'AuditLogs', 'COLUMN', N'TraceId';

DECLARE @description331 AS sql_variant;
SET @description331 = N'時間戳記';
EXEC sp_addextendedproperty 'MS_Description', @description331, 'SCHEMA', N'core', 'TABLE', N'AuditLogs', 'COLUMN', N'Timestamp';

DECLARE @description332 AS sql_variant;
SET @description332 = N'資料表名稱';
EXEC sp_addextendedproperty 'MS_Description', @description332, 'SCHEMA', N'core', 'TABLE', N'AuditLogs', 'COLUMN', N'TableName';

DECLARE @description333 AS sql_variant;
SET @description333 = N'儲存雜湊值';
EXEC sp_addextendedproperty 'MS_Description', @description333, 'SCHEMA', N'core', 'TABLE', N'AuditLogs', 'COLUMN', N'StoredHash';

DECLARE @description334 AS sql_variant;
SET @description334 = N'資料表綱要';
EXEC sp_addextendedproperty 'MS_Description', @description334, 'SCHEMA', N'core', 'TABLE', N'AuditLogs', 'COLUMN', N'Schema';

DECLARE @description335 AS sql_variant;
SET @description335 = N'修復時間';
EXEC sp_addextendedproperty 'MS_Description', @description335, 'SCHEMA', N'core', 'TABLE', N'AuditLogs', 'COLUMN', N'RepairedAt';

DECLARE @description336 AS sql_variant;
SET @description336 = N'遠端 IP 位址';
EXEC sp_addextendedproperty 'MS_Description', @description336, 'SCHEMA', N'core', 'TABLE', N'AuditLogs', 'COLUMN', N'RemoteIp';

DECLARE @description337 AS sql_variant;
SET @description337 = N'前一筆雜湊值';
EXEC sp_addextendedproperty 'MS_Description', @description337, 'SCHEMA', N'core', 'TABLE', N'AuditLogs', 'COLUMN', N'PreviousHash';

DECLARE @description338 AS sql_variant;
SET @description338 = N'原始儲存雜湊值';
EXEC sp_addextendedproperty 'MS_Description', @description338, 'SCHEMA', N'core', 'TABLE', N'AuditLogs', 'COLUMN', N'OriginalStoredHash';

DECLARE @description339 AS sql_variant;
SET @description339 = N'舊值';
EXEC sp_addextendedproperty 'MS_Description', @description339, 'SCHEMA', N'core', 'TABLE', N'AuditLogs', 'COLUMN', N'OldValues';

DECLARE @description340 AS sql_variant;
SET @description340 = N'新值';
EXEC sp_addextendedproperty 'MS_Description', @description340, 'SCHEMA', N'core', 'TABLE', N'AuditLogs', 'COLUMN', N'NewValues';

DECLARE @description341 AS sql_variant;
SET @description341 = N'主鍵值';
EXEC sp_addextendedproperty 'MS_Description', @description341, 'SCHEMA', N'core', 'TABLE', N'AuditLogs', 'COLUMN', N'KeyValues';

DECLARE @description342 AS sql_variant;
SET @description342 = N'是否已修復';
EXEC sp_addextendedproperty 'MS_Description', @description342, 'SCHEMA', N'core', 'TABLE', N'AuditLogs', 'COLUMN', N'IsRepaired';

DECLARE @description343 AS sql_variant;
SET @description343 = N'差異原因';
EXEC sp_addextendedproperty 'MS_Description', @description343, 'SCHEMA', N'core', 'TABLE', N'AuditLogs', 'COLUMN', N'GapReason';

DECLARE @description344 AS sql_variant;
SET @description344 = N'變更欄位';
EXEC sp_addextendedproperty 'MS_Description', @description344, 'SCHEMA', N'core', 'TABLE', N'AuditLogs', 'COLUMN', N'ChangedColumns';

DECLARE @description345 AS sql_variant;
SET @description345 = N'操作類型';
EXEC sp_addextendedproperty 'MS_Description', @description345, 'SCHEMA', N'core', 'TABLE', N'AuditLogs', 'COLUMN', N'Action';

DECLARE @description346 AS sql_variant;
SET @description346 = N'建立時間';
EXEC sp_addextendedproperty 'MS_Description', @description346, 'SCHEMA', N'core', 'TABLE', N'AuditLogs', 'COLUMN', N'CreatedAt';

DECLARE @description347 AS sql_variant;
SET @description347 = N'稽核日誌識別主鍵';
EXEC sp_addextendedproperty 'MS_Description', @description347, 'SCHEMA', N'core', 'TABLE', N'AuditLogs', 'COLUMN', N'Id';

DECLARE @description348 AS sql_variant;
SET @description348 = N'帳號目前狀態';
EXEC sp_addextendedproperty 'MS_Description', @description348, 'SCHEMA', N'core', 'TABLE', N'AspNetUsers', 'COLUMN', N'Status';

DECLARE @description349 AS sql_variant;
SET @description349 = N'帳號最後登入時間';
EXEC sp_addextendedproperty 'MS_Description', @description349, 'SCHEMA', N'core', 'TABLE', N'AspNetUsers', 'COLUMN', N'LastLoginAt';

DECLARE @description350 AS sql_variant;
SET @description350 = N'是否為系統管理員';
EXEC sp_addextendedproperty 'MS_Description', @description350, 'SCHEMA', N'core', 'TABLE', N'AspNetUsers', 'COLUMN', N'IsSystemAdmin';

DECLARE @description351 AS sql_variant;
SET @description351 = N'帳號建立者';
EXEC sp_addextendedproperty 'MS_Description', @description351, 'SCHEMA', N'core', 'TABLE', N'AspNetUsers', 'COLUMN', N'IsDeleted';

DECLARE @description352 AS sql_variant;
SET @description352 = N'使用者真實姓名';
EXEC sp_addextendedproperty 'MS_Description', @description352, 'SCHEMA', N'core', 'TABLE', N'AspNetUsers', 'COLUMN', N'FullName';

DECLARE @description353 AS sql_variant;
SET @description353 = N'員工編號';
EXEC sp_addextendedproperty 'MS_Description', @description353, 'SCHEMA', N'core', 'TABLE', N'AspNetUsers', 'COLUMN', N'EmployeeNo';

DECLARE @description354 AS sql_variant;
SET @description354 = N'帳號刪除者';
EXEC sp_addextendedproperty 'MS_Description', @description354, 'SCHEMA', N'core', 'TABLE', N'AspNetUsers', 'COLUMN', N'DeletedBy';

DECLARE @description355 AS sql_variant;
SET @description355 = N'帳號刪除時間';
EXEC sp_addextendedproperty 'MS_Description', @description355, 'SCHEMA', N'core', 'TABLE', N'AspNetUsers', 'COLUMN', N'DeletedAt';

DECLARE @description356 AS sql_variant;
SET @description356 = N'帳號建立時間';
EXEC sp_addextendedproperty 'MS_Description', @description356, 'SCHEMA', N'core', 'TABLE', N'AspNetUsers', 'COLUMN', N'CreatedAt';

DECLARE @description357 AS sql_variant;
SET @description357 = N'區域實驗室專屬角色的所屬租戶實驗室 ID（全域或系統角色則為 Null）';
EXEC sp_addextendedproperty 'MS_Description', @description357, 'SCHEMA', N'core', 'TABLE', N'AspNetRoles', 'COLUMN', N'TenantLabId';

DECLARE @description358 AS sql_variant;
SET @description358 = N'角色狀態(0 = 停用, 1 = 啟用中,2=已鎖定 ,3=待審核)';
EXEC sp_addextendedproperty 'MS_Description', @description358, 'SCHEMA', N'core', 'TABLE', N'AspNetRoles', 'COLUMN', N'Status';

DECLARE @description359 AS sql_variant;
SET @description359 = N'角色範圍類型 (SystemDefault, Global, LabScoped)';
EXEC sp_addextendedproperty 'MS_Description', @description359, 'SCHEMA', N'core', 'TABLE', N'AspNetRoles', 'COLUMN', N'ScopeType';

DECLARE @description360 AS sql_variant;
SET @description360 = N'是否為稽核角色。預設為 false；僅在使用者具有效「稽核指派」的區域實驗室中生效。';
EXEC sp_addextendedproperty 'MS_Description', @description360, 'SCHEMA', N'core', 'TABLE', N'AspNetRoles', 'COLUMN', N'RequiresAuditAssignment';

DECLARE @description361 AS sql_variant;
SET @description361 = N'是否為系統內建角色。預設為 false；系統初始化範本會明確設為 true。';
EXEC sp_addextendedproperty 'MS_Description', @description361, 'SCHEMA', N'core', 'TABLE', N'AspNetRoles', 'COLUMN', N'IsSystemRole';

DECLARE @description362 AS sql_variant;
SET @description362 = N'是否已刪除';
EXEC sp_addextendedproperty 'MS_Description', @description362, 'SCHEMA', N'core', 'TABLE', N'AspNetRoles', 'COLUMN', N'IsDeleted';

DECLARE @description363 AS sql_variant;
SET @description363 = N'角色管理階層數值（數字越大位階越高。例: SA=100, MO=50, MA=10）';
EXEC sp_addextendedproperty 'MS_Description', @description363, 'SCHEMA', N'core', 'TABLE', N'AspNetRoles', 'COLUMN', N'HierarchyLevel';

DECLARE @description364 AS sql_variant;
SET @description364 = N'生效迄日（含）；null 表示無期限';
EXEC sp_addextendedproperty 'MS_Description', @description364, 'SCHEMA', N'core', 'TABLE', N'AspNetRoles', 'COLUMN', N'EffectiveTo';

DECLARE @description365 AS sql_variant;
SET @description365 = N'生效起日（含）；null 表示立即生效。生效期間外角色不授予任何權限';
EXEC sp_addextendedproperty 'MS_Description', @description365, 'SCHEMA', N'core', 'TABLE', N'AspNetRoles', 'COLUMN', N'EffectiveFrom';

DECLARE @description366 AS sql_variant;
SET @description366 = N'角色描述';
EXEC sp_addextendedproperty 'MS_Description', @description366, 'SCHEMA', N'core', 'TABLE', N'AspNetRoles', 'COLUMN', N'Description';

DECLARE @description367 AS sql_variant;
SET @description367 = N'角色代碼';
EXEC sp_addextendedproperty 'MS_Description', @description367, 'SCHEMA', N'core', 'TABLE', N'AspNetRoles', 'COLUMN', N'Code';

DECLARE @description368 AS sql_variant;
SET @description368 = N'使用者帳號';
EXEC sp_addextendedproperty 'MS_Description', @description368, 'SCHEMA', N'core', 'TABLE', N'AccessReviewItems', 'COLUMN', N'UserName';

DECLARE @description369 AS sql_variant;
SET @description369 = N'覆核活動名稱';
EXEC sp_addextendedproperty 'MS_Description', @description369, 'SCHEMA', N'core', 'TABLE', N'AccessReviewItems', 'COLUMN', N'UserId';

DECLARE @description370 AS sql_variant;
SET @description370 = N'角色名稱';
EXEC sp_addextendedproperty 'MS_Description', @description370, 'SCHEMA', N'core', 'TABLE', N'AccessReviewItems', 'COLUMN', N'RoleName';

DECLARE @description371 AS sql_variant;
SET @description371 = N'角色識別';
EXEC sp_addextendedproperty 'MS_Description', @description371, 'SCHEMA', N'core', 'TABLE', N'AccessReviewItems', 'COLUMN', N'RoleId';

DECLARE @description372 AS sql_variant;
SET @description372 = N'快照：使用者主要實驗室名稱（覆核者依此範圍覆核所屬人員）';
EXEC sp_addextendedproperty 'MS_Description', @description372, 'SCHEMA', N'core', 'TABLE', N'AccessReviewItems', 'COLUMN', N'PrimaryLabName';

DECLARE @description373 AS sql_variant;
SET @description373 = N'快照：使用者主要實驗室（覆核者依此範圍覆核所屬人員）';
EXEC sp_addextendedproperty 'MS_Description', @description373, 'SCHEMA', N'core', 'TABLE', N'AccessReviewItems', 'COLUMN', N'PrimaryLabId';

DECLARE @description374 AS sql_variant;
SET @description374 = N'使用者全名';
EXEC sp_addextendedproperty 'MS_Description', @description374, 'SCHEMA', N'core', 'TABLE', N'AccessReviewItems', 'COLUMN', N'FullName';

DECLARE @description375 AS sql_variant;
SET @description375 = N'覆核決定';
EXEC sp_addextendedproperty 'MS_Description', @description375, 'SCHEMA', N'core', 'TABLE', N'AccessReviewItems', 'COLUMN', N'Decision';

DECLARE @description376 AS sql_variant;
SET @description376 = N'覆核決定人員名稱';
EXEC sp_addextendedproperty 'MS_Description', @description376, 'SCHEMA', N'core', 'TABLE', N'AccessReviewItems', 'COLUMN', N'DecidedByName';

DECLARE @description377 AS sql_variant;
SET @description377 = N'覆核決定時間';
EXEC sp_addextendedproperty 'MS_Description', @description377, 'SCHEMA', N'core', 'TABLE', N'AccessReviewItems', 'COLUMN', N'DecidedBy';

DECLARE @description378 AS sql_variant;
SET @description378 = N'覆核決定時間';
EXEC sp_addextendedproperty 'MS_Description', @description378, 'SCHEMA', N'core', 'TABLE', N'AccessReviewItems', 'COLUMN', N'DecidedAt';

DECLARE @description379 AS sql_variant;
SET @description379 = N'覆核決定說明';
EXEC sp_addextendedproperty 'MS_Description', @description379, 'SCHEMA', N'core', 'TABLE', N'AccessReviewItems', 'COLUMN', N'Comment';

DECLARE @description380 AS sql_variant;
SET @description380 = N'覆核活動識別';
EXEC sp_addextendedproperty 'MS_Description', @description380, 'SCHEMA', N'core', 'TABLE', N'AccessReviewItems', 'COLUMN', N'CampaignId';

DECLARE @description381 AS sql_variant;
SET @description381 = N'識別主鍵';
EXEC sp_addextendedproperty 'MS_Description', @description381, 'SCHEMA', N'core', 'TABLE', N'AccessReviewItems', 'COLUMN', N'Id';

ALTER TABLE [core].[AccessReviewItems] ADD [Email] nvarchar(max) NULL;
DECLARE @description382 AS sql_variant;
SET @description382 = N'使用者電子郵件';
EXEC sp_addextendedproperty 'MS_Description', @description382, 'SCHEMA', N'core', 'TABLE', N'AccessReviewItems', 'COLUMN', N'Email';

DECLARE @description383 AS sql_variant;
SET @description383 = N'活動狀態';
EXEC sp_addextendedproperty 'MS_Description', @description383, 'SCHEMA', N'core', 'TABLE', N'AccessReviewCampaigns', 'COLUMN', N'Status';

DECLARE @description384 AS sql_variant;
SET @description384 = N'覆核範圍的角色 ID（JSON 陣列）；空陣列表示全部角色';
EXEC sp_addextendedproperty 'MS_Description', @description384, 'SCHEMA', N'core', 'TABLE', N'AccessReviewCampaigns', 'COLUMN', N'RoleScope';

DECLARE @description385 AS sql_variant;
SET @description385 = N'覆核活動名稱';
EXEC sp_addextendedproperty 'MS_Description', @description385, 'SCHEMA', N'core', 'TABLE', N'AccessReviewCampaigns', 'COLUMN', N'Name';

DECLARE @description386 AS sql_variant;
SET @description386 = N'覆核期限';
EXEC sp_addextendedproperty 'MS_Description', @description386, 'SCHEMA', N'core', 'TABLE', N'AccessReviewCampaigns', 'COLUMN', N'DueDate';

DECLARE @description387 AS sql_variant;
SET @description387 = N'覆核活動描述';
EXEC sp_addextendedproperty 'MS_Description', @description387, 'SCHEMA', N'core', 'TABLE', N'AccessReviewCampaigns', 'COLUMN', N'Description';

DECLARE @description388 AS sql_variant;
SET @description388 = N'覆核範圍的實驗室 ID';
EXEC sp_addextendedproperty 'MS_Description', @description388, 'SCHEMA', N'core', 'TABLE', N'AccessReviewCampaigns', 'COLUMN', N'CreatedBy';

DECLARE @description389 AS sql_variant;
SET @description389 = N'建立時間';
EXEC sp_addextendedproperty 'MS_Description', @description389, 'SCHEMA', N'core', 'TABLE', N'AccessReviewCampaigns', 'COLUMN', N'CreatedAt';

DECLARE @description390 AS sql_variant;
SET @description390 = N'覆核結束人員';
EXEC sp_addextendedproperty 'MS_Description', @description390, 'SCHEMA', N'core', 'TABLE', N'AccessReviewCampaigns', 'COLUMN', N'ClosedBy';

DECLARE @description391 AS sql_variant;
SET @description391 = N'覆核結束時間';
EXEC sp_addextendedproperty 'MS_Description', @description391, 'SCHEMA', N'core', 'TABLE', N'AccessReviewCampaigns', 'COLUMN', N'ClosedAt';

DECLARE @description392 AS sql_variant;
SET @description392 = N'識別主鍵';
EXEC sp_addextendedproperty 'MS_Description', @description392, 'SCHEMA', N'core', 'TABLE', N'AccessReviewCampaigns', 'COLUMN', N'Id';

CREATE INDEX [IX_SystemLogs_UserId] ON [core].[SystemLogs] ([UserId]);

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261024162310_AddEntityComments', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
DECLARE @description393 AS sql_variant;
SET @description393 = N'儲存使用者裝置安全 Session 與長效憑證的資料表實體 (相容性與資安一體化規格)';
EXEC sp_addextendedproperty 'MS_Description', @description393, 'SCHEMA', N'core', 'TABLE', N'UserRefreshTokens';

DECLARE @description394 AS sql_variant;
SET @description394 = N'職能分立規則 (SoD, Separation of Duties)：兩項互斥權限不得同時存在於同一角色。例如「建立付款單」與「核准付款單」。';
EXEC sp_addextendedproperty 'MS_Description', @description394, 'SCHEMA', N'core', 'TABLE', N'SodRules';

DECLARE @description395 AS sql_variant;
SET @description395 = N'Outbox 訊息實體，用於實作「發佈者確認」模式 (Publisher Confirmed)';
EXEC sp_addextendedproperty 'MS_Description', @description395, 'SCHEMA', N'core', 'TABLE', N'OutboxMessages';

DECLARE @description396 AS sql_variant;
SET @description396 = N'使用者權杖：外部驗證提供者發給使用者的權杖 (如驗證器金鑰、復原碼)';
EXEC sp_addextendedproperty 'MS_Description', @description396, 'SCHEMA', N'core', 'TABLE', N'AspNetUserTokens';

DECLARE @description397 AS sql_variant;
SET @description397 = N'使用者角色指派：使用者與角色的多對多關聯';
EXEC sp_addextendedproperty 'MS_Description', @description397, 'SCHEMA', N'core', 'TABLE', N'AspNetUserRoles';

DECLARE @description398 AS sql_variant;
SET @description398 = N'使用者外部登入：使用者與外部驗證提供者 (如 Windows / SSO) 帳號的對應';
EXEC sp_addextendedproperty 'MS_Description', @description398, 'SCHEMA', N'core', 'TABLE', N'AspNetUserLogins';

DECLARE @description399 AS sql_variant;
SET @description399 = N'使用者宣告 (Claims)：附加於使用者的身分屬性，登入時寫入權杖';
EXEC sp_addextendedproperty 'MS_Description', @description399, 'SCHEMA', N'core', 'TABLE', N'AspNetUserClaims';

DECLARE @description400 AS sql_variant;
SET @description400 = N'角色宣告 (Claims)：附加於角色的身分屬性，該角色成員登入時一併取得';
EXEC sp_addextendedproperty 'MS_Description', @description400, 'SCHEMA', N'core', 'TABLE', N'AspNetRoleClaims';

DECLARE @description401 AS sql_variant;
SET @description401 = N'權限鍵 B';
EXEC sp_addextendedproperty 'MS_Description', @description401, 'SCHEMA', N'core', 'TABLE', N'SodRules', 'COLUMN', N'PermissionKeyB';

DECLARE @description402 AS sql_variant;
SET @description402 = N'權限鍵 A';
EXEC sp_addextendedproperty 'MS_Description', @description402, 'SCHEMA', N'core', 'TABLE', N'SodRules', 'COLUMN', N'PermissionKeyA';

DECLARE @description403 AS sql_variant;
SET @description403 = N'規則名稱';
EXEC sp_addextendedproperty 'MS_Description', @description403, 'SCHEMA', N'core', 'TABLE', N'SodRules', 'COLUMN', N'Name';

DECLARE @description404 AS sql_variant;
SET @description404 = N'是否啟用';
EXEC sp_addextendedproperty 'MS_Description', @description404, 'SCHEMA', N'core', 'TABLE', N'SodRules', 'COLUMN', N'IsEnabled';

DECLARE @description405 AS sql_variant;
SET @description405 = N'規則說明';
EXEC sp_addextendedproperty 'MS_Description', @description405, 'SCHEMA', N'core', 'TABLE', N'SodRules', 'COLUMN', N'Description';

DECLARE @description406 AS sql_variant;
SET @description406 = N'建立者';
EXEC sp_addextendedproperty 'MS_Description', @description406, 'SCHEMA', N'core', 'TABLE', N'SodRules', 'COLUMN', N'CreatedBy';

DECLARE @description407 AS sql_variant;
SET @description407 = N'建立時間';
EXEC sp_addextendedproperty 'MS_Description', @description407, 'SCHEMA', N'core', 'TABLE', N'SodRules', 'COLUMN', N'CreatedAt';

DECLARE @description408 AS sql_variant;
SET @description408 = N'識別主鍵';
EXEC sp_addextendedproperty 'MS_Description', @description408, 'SCHEMA', N'core', 'TABLE', N'SodRules', 'COLUMN', N'Id';

DECLARE @description409 AS sql_variant;
SET @description409 = N'事件的完整型別名稱 (包含 Namespace)，用於還原物件';
EXEC sp_addextendedproperty 'MS_Description', @description409, 'SCHEMA', N'core', 'TABLE', N'OutboxMessages', 'COLUMN', N'Type';

DECLARE @description410 AS sql_variant;
SET @description410 = N'預計下次重試的時間 (UTC)。用於實作「指數退避」，避免失敗後立即重試造成系統壓力。';
EXEC sp_addextendedproperty 'MS_Description', @description410, 'SCHEMA', N'core', 'TABLE', N'OutboxMessages', 'COLUMN', N'ScheduledAtUtc';

DECLARE @description411 AS sql_variant;
SET @description411 = N'目前已重試的次數';
EXEC sp_addextendedproperty 'MS_Description', @description411, 'SCHEMA', N'core', 'TABLE', N'OutboxMessages', 'COLUMN', N'RetryCount';

DECLARE @description412 AS sql_variant;
SET @description412 = N'訊息被成功處理的時間。若為 NULL 且 IsDead 為 false，表示待處理；若為 1900-01-01，表示正被某個 Worker 鎖定處理中。';
EXEC sp_addextendedproperty 'MS_Description', @description412, 'SCHEMA', N'core', 'TABLE', N'OutboxMessages', 'COLUMN', N'ProcessedOnUtc';

DECLARE @description413 AS sql_variant;
SET @description413 = N'事件發生的原始時間 (UTC)';
EXEC sp_addextendedproperty 'MS_Description', @description413, 'SCHEMA', N'core', 'TABLE', N'OutboxMessages', 'COLUMN', N'OccurredOnUtc';

DECLARE @description414 AS sql_variant;
SET @description414 = N'最後一次處理失敗的錯誤訊息內容，便於維運排錯';
EXEC sp_addextendedproperty 'MS_Description', @description414, 'SCHEMA', N'core', 'TABLE', N'OutboxMessages', 'COLUMN', N'LastError';

DECLARE @description415 AS sql_variant;
SET @description415 = N'當 RetryCount 超過閾值後標記為 true，背景服務將不再主動抓取。';
EXEC sp_addextendedproperty 'MS_Description', @description415, 'SCHEMA', N'core', 'TABLE', N'OutboxMessages', 'COLUMN', N'IsDead';

DECLARE @description416 AS sql_variant;
SET @description416 = N'關聯 ID：代表整個業務流程的追蹤編號 (由前端或 API 傳入)';
EXEC sp_addextendedproperty 'MS_Description', @description416, 'SCHEMA', N'core', 'TABLE', N'OutboxMessages', 'COLUMN', N'CorrelationId';

DECLARE @description417 AS sql_variant;
SET @description417 = N'事件序列化後的 JSON 內容';
EXEC sp_addextendedproperty 'MS_Description', @description417, 'SCHEMA', N'core', 'TABLE', N'OutboxMessages', 'COLUMN', N'Content';

DECLARE @description418 AS sql_variant;
SET @description418 = N'誘發 ID：代表觸發此事件的具體 Command 名稱或 ID';
EXEC sp_addextendedproperty 'MS_Description', @description418, 'SCHEMA', N'core', 'TABLE', N'OutboxMessages', 'COLUMN', N'CausationId';

DECLARE @description419 AS sql_variant;
SET @description419 = N'識別主鍵';
EXEC sp_addextendedproperty 'MS_Description', @description419, 'SCHEMA', N'core', 'TABLE', N'OutboxMessages', 'COLUMN', N'Id';

DECLARE @description420 AS sql_variant;
SET @description420 = N'權杖值';
EXEC sp_addextendedproperty 'MS_Description', @description420, 'SCHEMA', N'core', 'TABLE', N'AspNetUserTokens', 'COLUMN', N'Value';

DECLARE @description421 AS sql_variant;
SET @description421 = N'權杖名稱';
EXEC sp_addextendedproperty 'MS_Description', @description421, 'SCHEMA', N'core', 'TABLE', N'AspNetUserTokens', 'COLUMN', N'Name';

DECLARE @description422 AS sql_variant;
SET @description422 = N'權杖所屬驗證提供者';
EXEC sp_addextendedproperty 'MS_Description', @description422, 'SCHEMA', N'core', 'TABLE', N'AspNetUserTokens', 'COLUMN', N'LoginProvider';

DECLARE @description423 AS sql_variant;
SET @description423 = N'使用者識別 (AspNetUsers.Id)';
EXEC sp_addextendedproperty 'MS_Description', @description423, 'SCHEMA', N'core', 'TABLE', N'AspNetUserTokens', 'COLUMN', N'UserId';

DECLARE @description424 AS sql_variant;
SET @description424 = N'登入帳號';
EXEC sp_addextendedproperty 'MS_Description', @description424, 'SCHEMA', N'core', 'TABLE', N'AspNetUsers', 'COLUMN', N'UserName';

DECLARE @description425 AS sql_variant;
SET @description425 = N'是否啟用雙因素驗證';
EXEC sp_addextendedproperty 'MS_Description', @description425, 'SCHEMA', N'core', 'TABLE', N'AspNetUsers', 'COLUMN', N'TwoFactorEnabled';

DECLARE @description426 AS sql_variant;
SET @description426 = N'安全戳記；密碼或安全性設定變更時更新，使既有登入憑證失效';
EXEC sp_addextendedproperty 'MS_Description', @description426, 'SCHEMA', N'core', 'TABLE', N'AspNetUsers', 'COLUMN', N'SecurityStamp';

DECLARE @description427 AS sql_variant;
SET @description427 = N'電話號碼是否已驗證';
EXEC sp_addextendedproperty 'MS_Description', @description427, 'SCHEMA', N'core', 'TABLE', N'AspNetUsers', 'COLUMN', N'PhoneNumberConfirmed';

DECLARE @description428 AS sql_variant;
SET @description428 = N'電話號碼';
EXEC sp_addextendedproperty 'MS_Description', @description428, 'SCHEMA', N'core', 'TABLE', N'AspNetUsers', 'COLUMN', N'PhoneNumber';

DECLARE @description429 AS sql_variant;
SET @description429 = N'密碼雜湊值 (不儲存明碼)';
EXEC sp_addextendedproperty 'MS_Description', @description429, 'SCHEMA', N'core', 'TABLE', N'AspNetUsers', 'COLUMN', N'PasswordHash';

DECLARE @description430 AS sql_variant;
SET @description430 = N'正規化帳號 (大寫，供唯一性檢查與查詢比對)';
EXEC sp_addextendedproperty 'MS_Description', @description430, 'SCHEMA', N'core', 'TABLE', N'AspNetUsers', 'COLUMN', N'NormalizedUserName';

DECLARE @description431 AS sql_variant;
SET @description431 = N'正規化電子郵件 (大寫，供查詢比對)';
EXEC sp_addextendedproperty 'MS_Description', @description431, 'SCHEMA', N'core', 'TABLE', N'AspNetUsers', 'COLUMN', N'NormalizedEmail';

DECLARE @description432 AS sql_variant;
SET @description432 = N'帳號鎖定截止時間 (UTC)；null 表示未鎖定';
EXEC sp_addextendedproperty 'MS_Description', @description432, 'SCHEMA', N'core', 'TABLE', N'AspNetUsers', 'COLUMN', N'LockoutEnd';

DECLARE @description433 AS sql_variant;
SET @description433 = N'是否允許因登入失敗而鎖定帳號';
EXEC sp_addextendedproperty 'MS_Description', @description433, 'SCHEMA', N'core', 'TABLE', N'AspNetUsers', 'COLUMN', N'LockoutEnabled';

DECLARE @description434 AS sql_variant;
SET @description434 = N'電子郵件是否已驗證';
EXEC sp_addextendedproperty 'MS_Description', @description434, 'SCHEMA', N'core', 'TABLE', N'AspNetUsers', 'COLUMN', N'EmailConfirmed';

DECLARE @description435 AS sql_variant;
SET @description435 = N'電子郵件';
EXEC sp_addextendedproperty 'MS_Description', @description435, 'SCHEMA', N'core', 'TABLE', N'AspNetUsers', 'COLUMN', N'Email';

DECLARE @description436 AS sql_variant;
SET @description436 = N'並行控制戳記 (樂觀鎖定)';
EXEC sp_addextendedproperty 'MS_Description', @description436, 'SCHEMA', N'core', 'TABLE', N'AspNetUsers', 'COLUMN', N'ConcurrencyStamp';

DECLARE @description437 AS sql_variant;
SET @description437 = N'連續登入失敗次數 (登入成功後歸零)';
EXEC sp_addextendedproperty 'MS_Description', @description437, 'SCHEMA', N'core', 'TABLE', N'AspNetUsers', 'COLUMN', N'AccessFailedCount';

DECLARE @description438 AS sql_variant;
SET @description438 = N'使用者識別主鍵';
EXEC sp_addextendedproperty 'MS_Description', @description438, 'SCHEMA', N'core', 'TABLE', N'AspNetUsers', 'COLUMN', N'Id';

DECLARE @description439 AS sql_variant;
SET @description439 = N'角色識別 (AspNetRoles.Id)';
EXEC sp_addextendedproperty 'MS_Description', @description439, 'SCHEMA', N'core', 'TABLE', N'AspNetUserRoles', 'COLUMN', N'RoleId';

DECLARE @description440 AS sql_variant;
SET @description440 = N'使用者識別 (AspNetUsers.Id)';
EXEC sp_addextendedproperty 'MS_Description', @description440, 'SCHEMA', N'core', 'TABLE', N'AspNetUserRoles', 'COLUMN', N'UserId';

DECLARE @description441 AS sql_variant;
SET @description441 = N'使用者識別 (AspNetUsers.Id)';
EXEC sp_addextendedproperty 'MS_Description', @description441, 'SCHEMA', N'core', 'TABLE', N'AspNetUserLogins', 'COLUMN', N'UserId';

DECLARE @description442 AS sql_variant;
SET @description442 = N'外部提供者顯示名稱';
EXEC sp_addextendedproperty 'MS_Description', @description442, 'SCHEMA', N'core', 'TABLE', N'AspNetUserLogins', 'COLUMN', N'ProviderDisplayName';

DECLARE @description443 AS sql_variant;
SET @description443 = N'外部提供者中的使用者唯一識別';
EXEC sp_addextendedproperty 'MS_Description', @description443, 'SCHEMA', N'core', 'TABLE', N'AspNetUserLogins', 'COLUMN', N'ProviderKey';

DECLARE @description444 AS sql_variant;
SET @description444 = N'外部驗證提供者名稱';
EXEC sp_addextendedproperty 'MS_Description', @description444, 'SCHEMA', N'core', 'TABLE', N'AspNetUserLogins', 'COLUMN', N'LoginProvider';

DECLARE @description445 AS sql_variant;
SET @description445 = N'使用者識別 (AspNetUsers.Id)';
EXEC sp_addextendedproperty 'MS_Description', @description445, 'SCHEMA', N'core', 'TABLE', N'AspNetUserClaims', 'COLUMN', N'UserId';

DECLARE @description446 AS sql_variant;
SET @description446 = N'宣告值';
EXEC sp_addextendedproperty 'MS_Description', @description446, 'SCHEMA', N'core', 'TABLE', N'AspNetUserClaims', 'COLUMN', N'ClaimValue';

DECLARE @description447 AS sql_variant;
SET @description447 = N'宣告類型';
EXEC sp_addextendedproperty 'MS_Description', @description447, 'SCHEMA', N'core', 'TABLE', N'AspNetUserClaims', 'COLUMN', N'ClaimType';

DECLARE @description448 AS sql_variant;
SET @description448 = N'識別主鍵';
EXEC sp_addextendedproperty 'MS_Description', @description448, 'SCHEMA', N'core', 'TABLE', N'AspNetUserClaims', 'COLUMN', N'Id';

DECLARE @description449 AS sql_variant;
SET @description449 = N'正規化角色名稱 (大寫，供唯一性檢查與查詢比對)';
EXEC sp_addextendedproperty 'MS_Description', @description449, 'SCHEMA', N'core', 'TABLE', N'AspNetRoles', 'COLUMN', N'NormalizedName';

DECLARE @description450 AS sql_variant;
SET @description450 = N'角色名稱';
EXEC sp_addextendedproperty 'MS_Description', @description450, 'SCHEMA', N'core', 'TABLE', N'AspNetRoles', 'COLUMN', N'Name';

DECLARE @description451 AS sql_variant;
SET @description451 = N'並行控制戳記 (樂觀鎖定)';
EXEC sp_addextendedproperty 'MS_Description', @description451, 'SCHEMA', N'core', 'TABLE', N'AspNetRoles', 'COLUMN', N'ConcurrencyStamp';

DECLARE @description452 AS sql_variant;
SET @description452 = N'角色識別主鍵';
EXEC sp_addextendedproperty 'MS_Description', @description452, 'SCHEMA', N'core', 'TABLE', N'AspNetRoles', 'COLUMN', N'Id';

DECLARE @description453 AS sql_variant;
SET @description453 = N'角色識別 (AspNetRoles.Id)';
EXEC sp_addextendedproperty 'MS_Description', @description453, 'SCHEMA', N'core', 'TABLE', N'AspNetRoleClaims', 'COLUMN', N'RoleId';

DECLARE @description454 AS sql_variant;
SET @description454 = N'宣告值';
EXEC sp_addextendedproperty 'MS_Description', @description454, 'SCHEMA', N'core', 'TABLE', N'AspNetRoleClaims', 'COLUMN', N'ClaimValue';

DECLARE @description455 AS sql_variant;
SET @description455 = N'宣告類型';
EXEC sp_addextendedproperty 'MS_Description', @description455, 'SCHEMA', N'core', 'TABLE', N'AspNetRoleClaims', 'COLUMN', N'ClaimType';

DECLARE @description456 AS sql_variant;
SET @description456 = N'識別主鍵';
EXEC sp_addextendedproperty 'MS_Description', @description456, 'SCHEMA', N'core', 'TABLE', N'AspNetRoleClaims', 'COLUMN', N'Id';

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261025132820_AddIdentityComments', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
DECLARE @description457 AS sql_variant;
SET @description457 = N'當前有效的 Refresh Token 雜湊值 (SHA256，避免明文外洩)';
EXEC sp_addextendedproperty 'MS_Description', @description457, 'SCHEMA', N'core', 'TABLE', N'UserRefreshTokens', 'COLUMN', N'RefreshTokenHash';

DECLARE @description458 AS sql_variant;
SET @description458 = N'最後更新人員識別鍵';
EXEC sp_addextendedproperty 'MS_Description', @description458, 'SCHEMA', N'core', 'TABLE', N'UserLabMappings', 'COLUMN', N'UpdatedBy';

DECLARE @description459 AS sql_variant;
SET @description459 = N'最後更新人員識別鍵';
EXEC sp_addextendedproperty 'MS_Description', @description459, 'SCHEMA', N'core', 'TABLE', N'UserGlobalPermissions', 'COLUMN', N'UpdatedBy';

DECLARE @description460 AS sql_variant;
SET @description460 = N'最後更新時間 (UTC)';
EXEC sp_addextendedproperty 'MS_Description', @description460, 'SCHEMA', N'core', 'TABLE', N'UserGlobalPermissions', 'COLUMN', N'UpdatedAtUtc';

DECLARE @description461 AS sql_variant;
SET @description461 = N'建立人員識別鍵';
EXEC sp_addextendedproperty 'MS_Description', @description461, 'SCHEMA', N'core', 'TABLE', N'UserGlobalPermissions', 'COLUMN', N'CreatedBy';

DECLARE @description462 AS sql_variant;
SET @description462 = N'建立時間 (UTC)';
EXEC sp_addextendedproperty 'MS_Description', @description462, 'SCHEMA', N'core', 'TABLE', N'UserGlobalPermissions', 'COLUMN', N'CreatedAtUtc';

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261025133748_AddAuditFieldComments', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [core].[AspNetUsers] ADD [CreatedAtUtc] datetimeoffset NOT NULL DEFAULT '0001-01-01T00:00:00.0000000+00:00';
DECLARE @description463 AS sql_variant;
SET @description463 = N'建立時間 (UTC)';
EXEC sp_addextendedproperty 'MS_Description', @description463, 'SCHEMA', N'core', 'TABLE', N'AspNetUsers', 'COLUMN', N'CreatedAtUtc';

ALTER TABLE [core].[AspNetUsers] ADD [CreatedBy] nvarchar(256) NOT NULL DEFAULT N'';
DECLARE @description464 AS sql_variant;
SET @description464 = N'建立者';
EXEC sp_addextendedproperty 'MS_Description', @description464, 'SCHEMA', N'core', 'TABLE', N'AspNetUsers', 'COLUMN', N'CreatedBy';

ALTER TABLE [core].[AspNetUsers] ADD [UpdatedAtUtc] datetimeoffset NULL;
DECLARE @description465 AS sql_variant;
SET @description465 = N'最後修改時間 (UTC)';
EXEC sp_addextendedproperty 'MS_Description', @description465, 'SCHEMA', N'core', 'TABLE', N'AspNetUsers', 'COLUMN', N'UpdatedAtUtc';

ALTER TABLE [core].[AspNetUsers] ADD [UpdatedBy] nvarchar(256) NULL;
DECLARE @description466 AS sql_variant;
SET @description466 = N'最後修改者';
EXEC sp_addextendedproperty 'MS_Description', @description466, 'SCHEMA', N'core', 'TABLE', N'AspNetUsers', 'COLUMN', N'UpdatedBy';

ALTER TABLE [core].[AspNetRoles] ADD [CreatedAtUtc] datetimeoffset NOT NULL DEFAULT '0001-01-01T00:00:00.0000000+00:00';
DECLARE @description467 AS sql_variant;
SET @description467 = N'建立時間 (UTC)';
EXEC sp_addextendedproperty 'MS_Description', @description467, 'SCHEMA', N'core', 'TABLE', N'AspNetRoles', 'COLUMN', N'CreatedAtUtc';

ALTER TABLE [core].[AspNetRoles] ADD [CreatedBy] nvarchar(256) NOT NULL DEFAULT N'';
DECLARE @description468 AS sql_variant;
SET @description468 = N'建立者';
EXEC sp_addextendedproperty 'MS_Description', @description468, 'SCHEMA', N'core', 'TABLE', N'AspNetRoles', 'COLUMN', N'CreatedBy';

ALTER TABLE [core].[AspNetRoles] ADD [UpdatedAtUtc] datetimeoffset NULL;
DECLARE @description469 AS sql_variant;
SET @description469 = N'最後修改時間 (UTC)';
EXEC sp_addextendedproperty 'MS_Description', @description469, 'SCHEMA', N'core', 'TABLE', N'AspNetRoles', 'COLUMN', N'UpdatedAtUtc';

ALTER TABLE [core].[AspNetRoles] ADD [UpdatedBy] nvarchar(256) NULL;
DECLARE @description470 AS sql_variant;
SET @description470 = N'最後修改者';
EXEC sp_addextendedproperty 'MS_Description', @description470, 'SCHEMA', N'core', 'TABLE', N'AspNetRoles', 'COLUMN', N'UpdatedBy';

UPDATE [core].[AspNetUsers] SET [CreatedAtUtc] = [CreatedAt] WHERE [CreatedAtUtc] = '0001-01-01T00:00:00+00:00';

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261026150123_AddAuditableToUserRole', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [core].[AlertRules] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Description] nvarchar(500) NULL,
    [IsEnabled] bit NOT NULL,
    [Priority] int NOT NULL,
    [LogType] varchar(20) NOT NULL,
    [MinLevel] varchar(20) NOT NULL,
    [MessageContains] nvarchar(200) NULL,
    [ModuleContains] nvarchar(100) NULL,
    [OperationContains] nvarchar(100) NULL,
    [ExceptionContains] nvarchar(200) NULL,
    [Action] varchar(20) NOT NULL,
    [CooldownSeconds] int NULL,
    [Recipients] nvarchar(1000) NULL,
    [CreatedBy] nvarchar(256) NOT NULL,
    [CreatedAtUtc] datetimeoffset NOT NULL,
    [UpdatedBy] nvarchar(256) NULL,
    [UpdatedAtUtc] datetimeoffset NULL,
    CONSTRAINT [PK_AlertRules] PRIMARY KEY ([Id])
);
DECLARE @description471 AS sql_variant;
SET @description471 = N'告警規則：管理者可於執行期調整的日誌告警條件。依優先序由小到大比對，第一條符合者決定結果。';
EXEC sp_addextendedproperty 'MS_Description', @description471, 'SCHEMA', N'core', 'TABLE', N'AlertRules';
SET @description471 = N'識別主鍵';
EXEC sp_addextendedproperty 'MS_Description', @description471, 'SCHEMA', N'core', 'TABLE', N'AlertRules', 'COLUMN', N'Id';
SET @description471 = N'規則名稱（唯一）';
EXEC sp_addextendedproperty 'MS_Description', @description471, 'SCHEMA', N'core', 'TABLE', N'AlertRules', 'COLUMN', N'Name';
SET @description471 = N'規則說明';
EXEC sp_addextendedproperty 'MS_Description', @description471, 'SCHEMA', N'core', 'TABLE', N'AlertRules', 'COLUMN', N'Description';
SET @description471 = N'是否啟用；停用的規則不參與比對';
EXEC sp_addextendedproperty 'MS_Description', @description471, 'SCHEMA', N'core', 'TABLE', N'AlertRules', 'COLUMN', N'IsEnabled';
SET @description471 = N'優先序；數字越小越先比對';
EXEC sp_addextendedproperty 'MS_Description', @description471, 'SCHEMA', N'core', 'TABLE', N'AlertRules', 'COLUMN', N'Priority';
SET @description471 = N'套用的日誌類型：Any / System / Security';
EXEC sp_addextendedproperty 'MS_Description', @description471, 'SCHEMA', N'core', 'TABLE', N'AlertRules', 'COLUMN', N'LogType';
SET @description471 = N'最低日誌層級（Verbose / Debug / Information / Warning / Error / Fatal），含此層級以上才符合';
EXEC sp_addextendedproperty 'MS_Description', @description471, 'SCHEMA', N'core', 'TABLE', N'AlertRules', 'COLUMN', N'MinLevel';
SET @description471 = N'訊息包含的關鍵字（不分大小寫），可填事件碼如 SEC-911';
EXEC sp_addextendedproperty 'MS_Description', @description471, 'SCHEMA', N'core', 'TABLE', N'AlertRules', 'COLUMN', N'MessageContains';
SET @description471 = N'日誌的模組名稱包含此字串（不分大小寫）';
EXEC sp_addextendedproperty 'MS_Description', @description471, 'SCHEMA', N'core', 'TABLE', N'AlertRules', 'COLUMN', N'ModuleContains';
SET @description471 = N'日誌的操作名稱包含此字串（不分大小寫）';
EXEC sp_addextendedproperty 'MS_Description', @description471, 'SCHEMA', N'core', 'TABLE', N'AlertRules', 'COLUMN', N'OperationContains';
SET @description471 = N'例外型別名稱包含此字串（不分大小寫），例如 SqlException';
EXEC sp_addextendedproperty 'MS_Description', @description471, 'SCHEMA', N'core', 'TABLE', N'AlertRules', 'COLUMN', N'ExceptionContains';
SET @description471 = N'符合時的動作：Alert / Suppress';
EXEC sp_addextendedproperty 'MS_Description', @description471, 'SCHEMA', N'core', 'TABLE', N'AlertRules', 'COLUMN', N'Action';
SET @description471 = N'相同告警的冷卻秒數；空值沿用全域設定';
EXEC sp_addextendedproperty 'MS_Description', @description471, 'SCHEMA', N'core', 'TABLE', N'AlertRules', 'COLUMN', N'CooldownSeconds';
SET @description471 = N'此規則的收件人（逗號分隔 Email）；空值沿用全域收件人';
EXEC sp_addextendedproperty 'MS_Description', @description471, 'SCHEMA', N'core', 'TABLE', N'AlertRules', 'COLUMN', N'Recipients';
SET @description471 = N'建立者';
EXEC sp_addextendedproperty 'MS_Description', @description471, 'SCHEMA', N'core', 'TABLE', N'AlertRules', 'COLUMN', N'CreatedBy';
SET @description471 = N'建立時間 (UTC)';
EXEC sp_addextendedproperty 'MS_Description', @description471, 'SCHEMA', N'core', 'TABLE', N'AlertRules', 'COLUMN', N'CreatedAtUtc';
SET @description471 = N'最後修改者';
EXEC sp_addextendedproperty 'MS_Description', @description471, 'SCHEMA', N'core', 'TABLE', N'AlertRules', 'COLUMN', N'UpdatedBy';
SET @description471 = N'最後修改時間 (UTC)';
EXEC sp_addextendedproperty 'MS_Description', @description471, 'SCHEMA', N'core', 'TABLE', N'AlertRules', 'COLUMN', N'UpdatedAtUtc';

CREATE INDEX [IX_AlertRules_IsEnabled_Priority] ON [core].[AlertRules] ([IsEnabled], [Priority]);

CREATE UNIQUE INDEX [IX_AlertRules_Name] ON [core].[AlertRules] ([Name]);

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261027031536_AddAlertRules', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [core].[SystemAccountAuditLogs] (
    [Id] uniqueidentifier NOT NULL,
    [SystemAccountId] nvarchar(50) NOT NULL,
    [Action] nvarchar(32) NOT NULL,
    [Summary] nvarchar(2000) NOT NULL,
    [ActorUserId] uniqueidentifier NULL,
    [ActorName] nvarchar(256) NULL,
    [ClientIp] nvarchar(64) NULL,
    [CorrelationId] nvarchar(64) NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_SystemAccountAuditLogs] PRIMARY KEY ([Id])
);
DECLARE @description472 AS sql_variant;
SET @description472 = N'系統帳號稽核歷程（僅新增不修改，刪除帳號後仍保留）';
EXEC sp_addextendedproperty 'MS_Description', @description472, 'SCHEMA', N'core', 'TABLE', N'SystemAccountAuditLogs';
SET @description472 = N'識別主鍵';
EXEC sp_addextendedproperty 'MS_Description', @description472, 'SCHEMA', N'core', 'TABLE', N'SystemAccountAuditLogs', 'COLUMN', N'Id';
SET @description472 = N'系統帳號識別鍵';
EXEC sp_addextendedproperty 'MS_Description', @description472, 'SCHEMA', N'core', 'TABLE', N'SystemAccountAuditLogs', 'COLUMN', N'SystemAccountId';
SET @description472 = N'動作（Created、Updated、ScopesChanged、StatusChanged、SecretRotated、Deleted、TokenIssued、TokenDenied）';
EXEC sp_addextendedproperty 'MS_Description', @description472, 'SCHEMA', N'core', 'TABLE', N'SystemAccountAuditLogs', 'COLUMN', N'Action';
SET @description472 = N'可讀的異動摘要';
EXEC sp_addextendedproperty 'MS_Description', @description472, 'SCHEMA', N'core', 'TABLE', N'SystemAccountAuditLogs', 'COLUMN', N'Summary';
SET @description472 = N'操作者用戶識別鍵（權杖換發時為空）';
EXEC sp_addextendedproperty 'MS_Description', @description472, 'SCHEMA', N'core', 'TABLE', N'SystemAccountAuditLogs', 'COLUMN', N'ActorUserId';
SET @description472 = N'操作者名稱（權杖換發時為 client_id）';
EXEC sp_addextendedproperty 'MS_Description', @description472, 'SCHEMA', N'core', 'TABLE', N'SystemAccountAuditLogs', 'COLUMN', N'ActorName';
SET @description472 = N'客戶端 IP';
EXEC sp_addextendedproperty 'MS_Description', @description472, 'SCHEMA', N'core', 'TABLE', N'SystemAccountAuditLogs', 'COLUMN', N'ClientIp';
SET @description472 = N'請求追蹤碼（X-Correlation-ID）';
EXEC sp_addextendedproperty 'MS_Description', @description472, 'SCHEMA', N'core', 'TABLE', N'SystemAccountAuditLogs', 'COLUMN', N'CorrelationId';
SET @description472 = N'建立時間';
EXEC sp_addextendedproperty 'MS_Description', @description472, 'SCHEMA', N'core', 'TABLE', N'SystemAccountAuditLogs', 'COLUMN', N'CreatedAt';

CREATE TABLE [core].[SystemAccounts] (
    [Id] nvarchar(50) NOT NULL,
    [ClientId] nvarchar(100) NOT NULL,
    [SecretHash] nvarchar(255) NOT NULL,
    [SecretHint] nvarchar(16) NULL,
    [Name] nvarchar(100) NOT NULL,
    [Description] nvarchar(500) NULL,
    [Status] int NOT NULL,
    [AccessTokenLifetimeMinutes] int NOT NULL,
    [AllowedIpRanges] nvarchar(500) NULL,
    [SecretExpiresAt] datetimeoffset NULL,
    [SecretRotatedAt] datetimeoffset NOT NULL,
    [TokensNotBefore] datetimeoffset NOT NULL,
    [LastUsedAt] datetimeoffset NULL,
    [LastUsedIp] nvarchar(64) NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedByName] nvarchar(256) NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedByName] nvarchar(256) NULL,
    CONSTRAINT [PK_SystemAccounts] PRIMARY KEY ([Id])
);
DECLARE @description473 AS sql_variant;
SET @description473 = N'系統帳號（服務間通訊 M2M 身分），以 Client Credentials 換取短效 JWT，並以 Scope 授權';
EXEC sp_addextendedproperty 'MS_Description', @description473, 'SCHEMA', N'core', 'TABLE', N'SystemAccounts';
SET @description473 = N'識別主鍵（含 sys_ 前綴，例如 sys_hangfire_worker）';
EXEC sp_addextendedproperty 'MS_Description', @description473, 'SCHEMA', N'core', 'TABLE', N'SystemAccounts', 'COLUMN', N'Id';
SET @description473 = N'用戶端識別碼 client_id（例如 hangfire-job-runner）';
EXEC sp_addextendedproperty 'MS_Description', @description473, 'SCHEMA', N'core', 'TABLE', N'SystemAccounts', 'COLUMN', N'ClientId';
SET @description473 = N'client_secret 雜湊值（PBKDF2，不儲存明文）';
EXEC sp_addextendedproperty 'MS_Description', @description473, 'SCHEMA', N'core', 'TABLE', N'SystemAccounts', 'COLUMN', N'SecretHash';
SET @description473 = N'client_secret 末四碼，供維運人員比對部署中的金鑰版本';
EXEC sp_addextendedproperty 'MS_Description', @description473, 'SCHEMA', N'core', 'TABLE', N'SystemAccounts', 'COLUMN', N'SecretHint';
SET @description473 = N'顯示名稱';
EXEC sp_addextendedproperty 'MS_Description', @description473, 'SCHEMA', N'core', 'TABLE', N'SystemAccounts', 'COLUMN', N'Name';
SET @description473 = N'用途說明（使用的服務、負責人等）';
EXEC sp_addextendedproperty 'MS_Description', @description473, 'SCHEMA', N'core', 'TABLE', N'SystemAccounts', 'COLUMN', N'Description';
SET @description473 = N'狀態：0 停用、1 啟用、2 凍結';
EXEC sp_addextendedproperty 'MS_Description', @description473, 'SCHEMA', N'core', 'TABLE', N'SystemAccounts', 'COLUMN', N'Status';
SET @description473 = N'簽發之 Access Token 有效分鐘數';
EXEC sp_addextendedproperty 'MS_Description', @description473, 'SCHEMA', N'core', 'TABLE', N'SystemAccounts', 'COLUMN', N'AccessTokenLifetimeMinutes';
SET @description473 = N'允許換發權杖的來源 IP / CIDR（逗號分隔，空白表示不限制）';
EXEC sp_addextendedproperty 'MS_Description', @description473, 'SCHEMA', N'core', 'TABLE', N'SystemAccounts', 'COLUMN', N'AllowedIpRanges';
SET @description473 = N'client_secret 到期時間（空白表示不到期），到期後須輪替金鑰才能換發權杖';
EXEC sp_addextendedproperty 'MS_Description', @description473, 'SCHEMA', N'core', 'TABLE', N'SystemAccounts', 'COLUMN', N'SecretExpiresAt';
SET @description473 = N'client_secret 最近一次產生 / 輪替時間';
EXEC sp_addextendedproperty 'MS_Description', @description473, 'SCHEMA', N'core', 'TABLE', N'SystemAccounts', 'COLUMN', N'SecretRotatedAt';
SET @description473 = N'早於此時間簽發的權杖一律視為無效（撤銷既有權杖時更新）';
EXEC sp_addextendedproperty 'MS_Description', @description473, 'SCHEMA', N'core', 'TABLE', N'SystemAccounts', 'COLUMN', N'TokensNotBefore';
SET @description473 = N'最近一次成功換發權杖時間';
EXEC sp_addextendedproperty 'MS_Description', @description473, 'SCHEMA', N'core', 'TABLE', N'SystemAccounts', 'COLUMN', N'LastUsedAt';
SET @description473 = N'最近一次成功換發權杖的來源 IP';
EXEC sp_addextendedproperty 'MS_Description', @description473, 'SCHEMA', N'core', 'TABLE', N'SystemAccounts', 'COLUMN', N'LastUsedIp';
SET @description473 = N'建立時間';
EXEC sp_addextendedproperty 'MS_Description', @description473, 'SCHEMA', N'core', 'TABLE', N'SystemAccounts', 'COLUMN', N'CreatedAt';
SET @description473 = N'建立者名稱';
EXEC sp_addextendedproperty 'MS_Description', @description473, 'SCHEMA', N'core', 'TABLE', N'SystemAccounts', 'COLUMN', N'CreatedByName';
SET @description473 = N'更新時間';
EXEC sp_addextendedproperty 'MS_Description', @description473, 'SCHEMA', N'core', 'TABLE', N'SystemAccounts', 'COLUMN', N'UpdatedAt';
SET @description473 = N'更新者名稱';
EXEC sp_addextendedproperty 'MS_Description', @description473, 'SCHEMA', N'core', 'TABLE', N'SystemAccounts', 'COLUMN', N'UpdatedByName';

CREATE TABLE [core].[SystemAccountScopes] (
    [SystemAccountId] nvarchar(50) NOT NULL,
    [Scope] nvarchar(100) NOT NULL,
    [GrantedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_SystemAccountScopes] PRIMARY KEY ([SystemAccountId], [Scope]),
    CONSTRAINT [FK_SystemAccountScopes_SystemAccounts_SystemAccountId] FOREIGN KEY ([SystemAccountId]) REFERENCES [core].[SystemAccounts] ([Id]) ON DELETE CASCADE
);
DECLARE @description474 AS sql_variant;
SET @description474 = N'系統帳號被授予的操作範圍（Scope）';
EXEC sp_addextendedproperty 'MS_Description', @description474, 'SCHEMA', N'core', 'TABLE', N'SystemAccountScopes';
SET @description474 = N'系統帳號識別鍵';
EXEC sp_addextendedproperty 'MS_Description', @description474, 'SCHEMA', N'core', 'TABLE', N'SystemAccountScopes', 'COLUMN', N'SystemAccountId';
SET @description474 = N'操作範圍（例如 inventory.write）';
EXEC sp_addextendedproperty 'MS_Description', @description474, 'SCHEMA', N'core', 'TABLE', N'SystemAccountScopes', 'COLUMN', N'Scope';
SET @description474 = N'授予時間';
EXEC sp_addextendedproperty 'MS_Description', @description474, 'SCHEMA', N'core', 'TABLE', N'SystemAccountScopes', 'COLUMN', N'GrantedAt';

CREATE INDEX [IX_SystemAccountAuditLogs_CreatedAt] ON [core].[SystemAccountAuditLogs] ([CreatedAt]);

CREATE INDEX [IX_SystemAccountAuditLogs_SystemAccountId_CreatedAt] ON [core].[SystemAccountAuditLogs] ([SystemAccountId], [CreatedAt]);

CREATE UNIQUE INDEX [IX_SystemAccounts_ClientId] ON [core].[SystemAccounts] ([ClientId]);

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261027053724_AddSystemAccounts', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [core].[ScheduledJobRuns] (
    [Id] uniqueidentifier NOT NULL,
    [JobId] nvarchar(100) NOT NULL,
    [JobName] nvarchar(200) NOT NULL,
    [HangfireJobId] nvarchar(50) NOT NULL,
    [Attempt] int NOT NULL,
    [Trigger] nvarchar(20) NOT NULL,
    [TriggeredBy] nvarchar(256) NULL,
    [Status] nvarchar(20) NOT NULL,
    [StartedAt] datetimeoffset NOT NULL,
    [FinishedAt] datetimeoffset NULL,
    [DurationMs] bigint NULL,
    [Message] nvarchar(2000) NULL,
    [ServerName] nvarchar(200) NULL,
    CONSTRAINT [PK_ScheduledJobRuns] PRIMARY KEY ([Id])
);
DECLARE @description475 AS sql_variant;
SET @description475 = N'排程作業執行紀錄：每次嘗試一筆（重試為新的一筆）';
EXEC sp_addextendedproperty 'MS_Description', @description475, 'SCHEMA', N'core', 'TABLE', N'ScheduledJobRuns';
SET @description475 = N'識別主鍵';
EXEC sp_addextendedproperty 'MS_Description', @description475, 'SCHEMA', N'core', 'TABLE', N'ScheduledJobRuns', 'COLUMN', N'Id';
SET @description475 = N'週期作業 ID（例如 database-backup-full）';
EXEC sp_addextendedproperty 'MS_Description', @description475, 'SCHEMA', N'core', 'TABLE', N'ScheduledJobRuns', 'COLUMN', N'JobId';
SET @description475 = N'執行當下的作業顯示名稱';
EXEC sp_addextendedproperty 'MS_Description', @description475, 'SCHEMA', N'core', 'TABLE', N'ScheduledJobRuns', 'COLUMN', N'JobName';
SET @description475 = N'Hangfire 背景作業 ID（同一作業的重試共用）';
EXEC sp_addextendedproperty 'MS_Description', @description475, 'SCHEMA', N'core', 'TABLE', N'ScheduledJobRuns', 'COLUMN', N'HangfireJobId';
SET @description475 = N'第幾次嘗試（1 = 首次執行）';
EXEC sp_addextendedproperty 'MS_Description', @description475, 'SCHEMA', N'core', 'TABLE', N'ScheduledJobRuns', 'COLUMN', N'Attempt';
SET @description475 = N'觸發方式：Schedule（排程）或 Manual（手動立即執行）';
EXEC sp_addextendedproperty 'MS_Description', @description475, 'SCHEMA', N'core', 'TABLE', N'ScheduledJobRuns', 'COLUMN', N'Trigger';
SET @description475 = N'手動執行的人員帳號；排程觸發為 null';
EXEC sp_addextendedproperty 'MS_Description', @description475, 'SCHEMA', N'core', 'TABLE', N'ScheduledJobRuns', 'COLUMN', N'TriggeredBy';
SET @description475 = N'執行狀態：Running / Succeeded / Failed / Retrying';
EXEC sp_addextendedproperty 'MS_Description', @description475, 'SCHEMA', N'core', 'TABLE', N'ScheduledJobRuns', 'COLUMN', N'Status';
SET @description475 = N'開始時間';
EXEC sp_addextendedproperty 'MS_Description', @description475, 'SCHEMA', N'core', 'TABLE', N'ScheduledJobRuns', 'COLUMN', N'StartedAt';
SET @description475 = N'結束時間';
EXEC sp_addextendedproperty 'MS_Description', @description475, 'SCHEMA', N'core', 'TABLE', N'ScheduledJobRuns', 'COLUMN', N'FinishedAt';
SET @description475 = N'耗時（毫秒）';
EXEC sp_addextendedproperty 'MS_Description', @description475, 'SCHEMA', N'core', 'TABLE', N'ScheduledJobRuns', 'COLUMN', N'DurationMs';
SET @description475 = N'失敗原因或重試說明';
EXEC sp_addextendedproperty 'MS_Description', @description475, 'SCHEMA', N'core', 'TABLE', N'ScheduledJobRuns', 'COLUMN', N'Message';
SET @description475 = N'執行作業的伺服器（多節點時追查用）';
EXEC sp_addextendedproperty 'MS_Description', @description475, 'SCHEMA', N'core', 'TABLE', N'ScheduledJobRuns', 'COLUMN', N'ServerName';

CREATE TABLE [core].[ScheduledJobSettings] (
    [JobId] nvarchar(100) NOT NULL,
    [IsPaused] bit NOT NULL,
    [PauseReason] nvarchar(500) NULL,
    [CreatedBy] nvarchar(256) NOT NULL,
    [CreatedAtUtc] datetimeoffset NOT NULL,
    [UpdatedBy] nvarchar(256) NULL,
    [UpdatedAtUtc] datetimeoffset NULL,
    CONSTRAINT [PK_ScheduledJobSettings] PRIMARY KEY ([JobId])
);
DECLARE @description476 AS sql_variant;
SET @description476 = N'排程作業的執行期設定：由排程管理頁暫停 / 啟動，系統重啟後仍維持';
EXEC sp_addextendedproperty 'MS_Description', @description476, 'SCHEMA', N'core', 'TABLE', N'ScheduledJobSettings';
SET @description476 = N'週期作業 ID（例如 database-backup-full）';
EXEC sp_addextendedproperty 'MS_Description', @description476, 'SCHEMA', N'core', 'TABLE', N'ScheduledJobSettings', 'COLUMN', N'JobId';
SET @description476 = N'是否暫停自動排程（暫停時仍可手動立即執行）';
EXEC sp_addextendedproperty 'MS_Description', @description476, 'SCHEMA', N'core', 'TABLE', N'ScheduledJobSettings', 'COLUMN', N'IsPaused';
SET @description476 = N'暫停原因';
EXEC sp_addextendedproperty 'MS_Description', @description476, 'SCHEMA', N'core', 'TABLE', N'ScheduledJobSettings', 'COLUMN', N'PauseReason';
SET @description476 = N'建立人員';
EXEC sp_addextendedproperty 'MS_Description', @description476, 'SCHEMA', N'core', 'TABLE', N'ScheduledJobSettings', 'COLUMN', N'CreatedBy';
SET @description476 = N'建立時間';
EXEC sp_addextendedproperty 'MS_Description', @description476, 'SCHEMA', N'core', 'TABLE', N'ScheduledJobSettings', 'COLUMN', N'CreatedAtUtc';
SET @description476 = N'最後暫停 / 啟動的人員';
EXEC sp_addextendedproperty 'MS_Description', @description476, 'SCHEMA', N'core', 'TABLE', N'ScheduledJobSettings', 'COLUMN', N'UpdatedBy';
SET @description476 = N'最後暫停 / 啟動的時間';
EXEC sp_addextendedproperty 'MS_Description', @description476, 'SCHEMA', N'core', 'TABLE', N'ScheduledJobSettings', 'COLUMN', N'UpdatedAtUtc';

CREATE UNIQUE INDEX [IX_ScheduledJobRuns_HangfireJobId_Attempt] ON [core].[ScheduledJobRuns] ([HangfireJobId], [Attempt]);

CREATE INDEX [IX_ScheduledJobRuns_JobId_StartedAt] ON [core].[ScheduledJobRuns] ([JobId], [StartedAt]);

CREATE INDEX [IX_ScheduledJobRuns_StartedAt] ON [core].[ScheduledJobRuns] ([StartedAt]);

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261028035521_AddScheduledJobRuns', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [core].[ScheduledJobSettings] ADD [Cron] nvarchar(100) NULL;
DECLARE @description477 AS sql_variant;
SET @description477 = N'排程管理頁設定的 Cron（5 碼）；null 表示沿用組態 / 程式預設';
EXEC sp_addextendedproperty 'MS_Description', @description477, 'SCHEMA', N'core', 'TABLE', N'ScheduledJobSettings', 'COLUMN', N'Cron';

ALTER TABLE [core].[ScheduledJobSettings] ADD [TimeZone] nvarchar(100) NULL;
DECLARE @description478 AS sql_variant;
SET @description478 = N'排程管理頁設定的時區（IANA 或 Windows ID）；null 表示沿用組態預設';
EXEC sp_addextendedproperty 'MS_Description', @description478, 'SCHEMA', N'core', 'TABLE', N'ScheduledJobSettings', 'COLUMN', N'TimeZone';

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261028052335_AddScheduledJobScheduleOverride', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
DECLARE @var479 nvarchar(max);
SELECT @var479 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[SystemLogs]') AND [c].[name] = N'Operation');
IF @var479 IS NOT NULL EXEC(N'ALTER TABLE [core].[SystemLogs] DROP CONSTRAINT ' + @var479 + ';');
ALTER TABLE [core].[SystemLogs] ALTER COLUMN [Operation] nvarchar(100) NULL;

ALTER TABLE [core].[SystemLogs] ADD [OperationDesc] nvarchar(255) NULL;
DECLARE @description480 AS sql_variant;
SET @description480 = N'操作說明 (ControllerTitle > FunctionTitle)';
EXEC sp_addextendedproperty 'MS_Description', @description480, 'SCHEMA', N'core', 'TABLE', N'SystemLogs', 'COLUMN', N'OperationDesc';

ALTER TABLE [core].[SystemLogs] ADD [UserName] nvarchar(100) NULL;
DECLARE @description481 AS sql_variant;
SET @description481 = N'人員名稱';
EXEC sp_addextendedproperty 'MS_Description', @description481, 'SCHEMA', N'core', 'TABLE', N'SystemLogs', 'COLUMN', N'UserName';

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261029081246_update_systemlog_add_operationdesc', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [core].[SystemLogs] ADD [TenantName] nvarchar(100) NULL;
DECLARE @description482 AS sql_variant;
SET @description482 = N'實驗室名稱 (TenantName)';
EXEC sp_addextendedproperty 'MS_Description', @description482, 'SCHEMA', N'core', 'TABLE', N'SystemLogs', 'COLUMN', N'TenantName';

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261029105016_update_systemlog_labname', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [core].[SystemLogs] ADD [ControllerName] nvarchar(100) NULL;
DECLARE @description483 AS sql_variant;
SET @description483 = N'MVC Controller 名稱';
EXEC sp_addextendedproperty 'MS_Description', @description483, 'SCHEMA', N'core', 'TABLE', N'SystemLogs', 'COLUMN', N'ControllerName';

INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261030021801_systemlog_add_controllerName', N'10.0.12');

COMMIT;
GO

