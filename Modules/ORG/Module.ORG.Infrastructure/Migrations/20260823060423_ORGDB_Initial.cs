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
            ");

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
                    NodePath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
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
                    table.CheckConstraint("CK_Organization_TenantLabId_Level2Only", "([TenantLabId] IS NULL) OR ([Level] = 2)");
                    table.ForeignKey(
                        name: "FK_Organization_Organization_ParentId",
                        column: x => x.ParentId,
                        principalSchema: "org",
                        principalTable: "Organization",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "org",
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
                name: "UserLaboratory",
                schema: "org",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLaboratory", x => new { x.UserId, x.OrganizationId });
                    table.ForeignKey(
                        name: "FK_UserLaboratory_Organization_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "org",
                        principalTable: "Organization",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "IX_Organization_ParentId",
                schema: "org",
                table: "Organization",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Organization_TenantLabId",
                schema: "org",
                table: "Organization",
                column: "TenantLabId",
                filter: "[TenantLabId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_org_OutboxMessages_CorrelationId",
                schema: "org",
                table: "OutboxMessages",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_org_OutboxMessages_FetchPending",
                schema: "org",
                table: "OutboxMessages",
                columns: new[] { "ProcessedOnUtc", "IsDead", "ScheduledAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UserLaboratory_OrganizationId",
                schema: "org",
                table: "UserLaboratory",
                column: "OrganizationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs",
                schema: "org");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "org");

            migrationBuilder.DropTable(
                name: "UserLaboratory",
                schema: "org");

            migrationBuilder.DropTable(
                name: "Organization",
                schema: "org");
        }
    }
}
