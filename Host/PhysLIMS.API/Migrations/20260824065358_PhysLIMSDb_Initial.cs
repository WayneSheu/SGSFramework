using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class PhysLIMSDb_Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "core");

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastLoginAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.Sql(@"
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
            ");


            migrationBuilder.CreateTable(
                name: "ControllerMetadatas",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModuleName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ModuleTitle = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ControllerTitle = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ControllerName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ControllerTypeName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActionName = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RouteTemplate = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Icon = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    ParentMenuName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PermissionKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControllerMetadatas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MenuItems",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ControllerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Icon = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Route = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    PermissionKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenuItems_MenuItems_ParentId",
                        column: x => x.ParentId,
                        principalSchema: "core",
                        principalTable: "MenuItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ModuleMetadatas",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModuleTitle = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModuleName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "1.0.0"),
                    AssemblyPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LastLoadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Checksum = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModuleMetadatas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrelationId = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    CausationId = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true),
                    Type = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OccurredOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ScheduledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsDead = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LastError = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PermissionGrants",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LabId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermissionVector = table.Column<byte[]>(type: "varbinary(64)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermissionGrants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PermissionKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    BitPosition = table.Column<int>(type: "int", nullable: false),
                    ModuleName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ControllerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActionName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RemediationTickets",
                schema: "core",
                columns: table => new
                {
                    TicketId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    IsUsed = table.Column<bool>(type: "bit", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RemediationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByIp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemediationTickets", x => x.TicketId);
                });

            migrationBuilder.Sql(@"
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
            ");


            migrationBuilder.Sql(@"
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
            ");


            migrationBuilder.CreateTable(
                name: "UserRefreshTokens",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeviceId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    DeviceName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RefreshTokenHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    RotatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReusedTokenCache = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastActiveAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClientIp = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsDead = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsFrozen = table.Column<bool>(type: "bit", nullable: false),
                    RiskReason = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRefreshTokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "core",
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "core",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                schema: "core",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "core",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                schema: "core",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "core",
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "core",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                schema: "core",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "core",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserLabMappings",
                schema: "core",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LabId = table.Column<int>(type: "int", nullable: false),
                    TenantLabId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    JobTitle = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLabMappings", x => new { x.UserId, x.LabId });
                    table.ForeignKey(
                        name: "FK_UserLabMappings_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "core",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserResourceGrants",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MenuItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LabId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermissionType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EffectType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    DelegatorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ValidFrom = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ValidTo = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserResourceGrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserResourceGrants_MenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalSchema: "core",
                        principalTable: "MenuItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                schema: "core",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                schema: "core",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                schema: "core",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                schema: "core",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                schema: "core",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                schema: "core",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                schema: "core",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_IsRepaired",
                schema: "core",
                table: "AuditLogs",
                column: "IsRepaired",
                filter: "[IsRepaired] = 0")
                .Annotation("SqlServer:Include", new[] { "TableName", "TraceId", "GapReason" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_TraceId_Covering",
                schema: "core",
                table: "AuditLogs",
                column: "TraceId")
                .Annotation("SqlServer:Include", new[] { "Action", "CreatedAt", "TableName" });

            migrationBuilder.CreateIndex(
                name: "IX_Metadata_ModuleName",
                schema: "core",
                table: "ControllerMetadatas",
                column: "ModuleName");

            migrationBuilder.CreateIndex(
                name: "IX_Metadata_PermissionKey",
                schema: "core",
                table: "ControllerMetadatas",
                column: "PermissionKey");

            migrationBuilder.CreateIndex(
                name: "UX_Module_Controller_Action",
                schema: "core",
                table: "ControllerMetadatas",
                columns: new[] { "ModuleName", "ControllerName", "ActionName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_DisplayOrder",
                schema: "core",
                table: "MenuItems",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_ParentId",
                schema: "core",
                table: "MenuItems",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_ModuleMetadatas_ModuleName",
                schema: "core",
                table: "ModuleMetadatas",
                column: "ModuleName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_core_OutboxMessages_CorrelationId",
                schema: "core",
                table: "OutboxMessages",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_core_OutboxMessages_FetchPending",
                schema: "core",
                table: "OutboxMessages",
                columns: new[] { "ProcessedOnUtc", "IsDead", "ScheduledAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PermissionGrant_Role_Lab",
                schema: "core",
                table: "PermissionGrants",
                columns: new[] { "RoleId", "LabId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_BitPosition",
                schema: "core",
                table: "Permissions",
                column: "BitPosition",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_PermissionKey",
                schema: "core",
                table: "Permissions",
                column: "PermissionKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RemediationTickets_TicketId_UserId",
                schema: "core",
                table: "RemediationTickets",
                columns: new[] { "TicketId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityLog_CorrelationId",
                schema: "core",
                table: "SecurityLog",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemLogs_CorrelationId",
                schema: "core",
                table: "SystemLogs",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemLogs_TenantId",
                schema: "core",
                table: "SystemLogs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemLogs_TimeStamp",
                schema: "core",
                table: "SystemLogs",
                column: "TimeStamp");

            migrationBuilder.CreateIndex(
                name: "IX_UserLabMappings_EffectiveRange",
                schema: "core",
                table: "UserLabMappings",
                columns: new[] { "UserId", "IsActive", "EffectiveDate", "ExpiryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_UserLabMappings_TenantLabId_UserId",
                schema: "core",
                table: "UserLabMappings",
                columns: new[] { "TenantLabId", "UserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "UX_UserLabMappings_OnePrimaryPerUser",
                schema: "core",
                table: "UserLabMappings",
                column: "UserId",
                unique: true,
                filter: "[IsPrimary] = 1 AND [IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_UserRefreshTokens_RefreshTokenHash",
                schema: "core",
                table: "UserRefreshTokens",
                column: "RefreshTokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_UserRefreshTokens_UserId_DeviceId_IsDead",
                schema: "core",
                table: "UserRefreshTokens",
                columns: new[] { "UserId", "DeviceId", "IsDead" });

            migrationBuilder.CreateIndex(
                name: "UX_UserRefreshTokens_UserId_DeviceId",
                schema: "core",
                table: "UserRefreshTokens",
                columns: new[] { "UserId", "DeviceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserResource_Lookup",
                schema: "core",
                table: "UserResourceGrants",
                columns: new[] { "UserId", "MenuItemId", "LabId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserResourceGrants_MenuItemId",
                schema: "core",
                table: "UserResourceGrants",
                column: "MenuItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims",
                schema: "core");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims",
                schema: "core");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins",
                schema: "core");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles",
                schema: "core");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens",
                schema: "core");

            migrationBuilder.DropTable(
                name: "AuditLogs",
                schema: "core");

            migrationBuilder.DropTable(
                name: "ControllerMetadatas",
                schema: "core");

            migrationBuilder.DropTable(
                name: "ModuleMetadatas",
                schema: "core");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "core");

            migrationBuilder.DropTable(
                name: "PermissionGrants",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Permissions",
                schema: "core");

            migrationBuilder.DropTable(
                name: "RemediationTickets",
                schema: "core");

            migrationBuilder.DropTable(
                name: "SecurityLog",
                schema: "core")
                .Annotation("SqlServer:IsLedger", true)
                .Annotation("SqlServer:IsLedgerAppendOnly", true);

            migrationBuilder.DropTable(
                name: "SystemLogs",
                schema: "core")
                .Annotation("SqlServer:IsLedger", true)
                .Annotation("SqlServer:IsLedgerAppendOnly", true);

            migrationBuilder.DropTable(
                name: "UserLabMappings",
                schema: "core");

            migrationBuilder.DropTable(
                name: "UserRefreshTokens",
                schema: "core");

            migrationBuilder.DropTable(
                name: "UserResourceGrants",
                schema: "core");

            migrationBuilder.DropTable(
                name: "AspNetRoles",
                schema: "core");

            migrationBuilder.DropTable(
                name: "AspNetUsers",
                schema: "core");

            migrationBuilder.DropTable(
                name: "MenuItems",
                schema: "core");
        }
    }
}
