using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SystemAccountAuditLogs",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "識別主鍵"),
                    SystemAccountId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, comment: "系統帳號識別鍵"),
                    Action = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, comment: "動作（Created、Updated、ScopesChanged、StatusChanged、SecretRotated、Deleted、TokenIssued、TokenDenied）"),
                    Summary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false, comment: "可讀的異動摘要"),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "操作者用戶識別鍵（權杖換發時為空）"),
                    ActorName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true, comment: "操作者名稱（權杖換發時為 client_id）"),
                    ClientIp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, comment: "客戶端 IP"),
                    CorrelationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, comment: "請求追蹤碼（X-Correlation-ID）"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "建立時間")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemAccountAuditLogs", x => x.Id);
                },
                comment: "系統帳號稽核歷程（僅新增不修改，刪除帳號後仍保留）");

            migrationBuilder.CreateTable(
                name: "SystemAccounts",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, comment: "識別主鍵（含 sys_ 前綴，例如 sys_hangfire_worker）"),
                    ClientId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, comment: "用戶端識別碼 client_id（例如 hangfire-job-runner）"),
                    SecretHash = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, comment: "client_secret 雜湊值（PBKDF2，不儲存明文）"),
                    SecretHint = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true, comment: "client_secret 末四碼，供維運人員比對部署中的金鑰版本"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, comment: "顯示名稱"),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true, comment: "用途說明（使用的服務、負責人等）"),
                    Status = table.Column<int>(type: "int", nullable: false, comment: "狀態：0 停用、1 啟用、2 凍結"),
                    AccessTokenLifetimeMinutes = table.Column<int>(type: "int", nullable: false, comment: "簽發之 Access Token 有效分鐘數"),
                    AllowedIpRanges = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true, comment: "允許換發權杖的來源 IP / CIDR（逗號分隔，空白表示不限制）"),
                    SecretExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "client_secret 到期時間（空白表示不到期），到期後須輪替金鑰才能換發權杖"),
                    SecretRotatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "client_secret 最近一次產生 / 輪替時間"),
                    TokensNotBefore = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "早於此時間簽發的權杖一律視為無效（撤銷既有權杖時更新）"),
                    LastUsedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "最近一次成功換發權杖時間"),
                    LastUsedIp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, comment: "最近一次成功換發權杖的來源 IP"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "建立時間"),
                    CreatedByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true, comment: "建立者名稱"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "更新時間"),
                    UpdatedByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true, comment: "更新者名稱")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemAccounts", x => x.Id);
                },
                comment: "系統帳號（服務間通訊 M2M 身分），以 Client Credentials 換取短效 JWT，並以 Scope 授權");

            migrationBuilder.CreateTable(
                name: "SystemAccountScopes",
                schema: "core",
                columns: table => new
                {
                    SystemAccountId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, comment: "系統帳號識別鍵"),
                    Scope = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, comment: "操作範圍（例如 inventory.write）"),
                    GrantedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "授予時間")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemAccountScopes", x => new { x.SystemAccountId, x.Scope });
                    table.ForeignKey(
                        name: "FK_SystemAccountScopes_SystemAccounts_SystemAccountId",
                        column: x => x.SystemAccountId,
                        principalSchema: "core",
                        principalTable: "SystemAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "系統帳號被授予的操作範圍（Scope）");

            migrationBuilder.CreateIndex(
                name: "IX_SystemAccountAuditLogs_CreatedAt",
                schema: "core",
                table: "SystemAccountAuditLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SystemAccountAuditLogs_SystemAccountId_CreatedAt",
                schema: "core",
                table: "SystemAccountAuditLogs",
                columns: new[] { "SystemAccountId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SystemAccounts_ClientId",
                schema: "core",
                table: "SystemAccounts",
                column: "ClientId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SystemAccountAuditLogs",
                schema: "core");

            migrationBuilder.DropTable(
                name: "SystemAccountScopes",
                schema: "core");

            migrationBuilder.DropTable(
                name: "SystemAccounts",
                schema: "core");
        }
    }
}
