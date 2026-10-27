using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class AddAlertRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AlertRules",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "識別主鍵"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, comment: "規則名稱（唯一）"),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true, comment: "規則說明"),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false, comment: "是否啟用；停用的規則不參與比對"),
                    Priority = table.Column<int>(type: "int", nullable: false, comment: "優先序；數字越小越先比對"),
                    LogType = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, comment: "套用的日誌類型：Any / System / Security"),
                    MinLevel = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, comment: "最低日誌層級（Verbose / Debug / Information / Warning / Error / Fatal），含此層級以上才符合"),
                    MessageContains = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true, comment: "訊息包含的關鍵字（不分大小寫），可填事件碼如 SEC-911"),
                    ModuleContains = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true, comment: "日誌的模組名稱包含此字串（不分大小寫）"),
                    OperationContains = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true, comment: "日誌的操作名稱包含此字串（不分大小寫）"),
                    ExceptionContains = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true, comment: "例外型別名稱包含此字串（不分大小寫），例如 SqlException"),
                    Action = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, comment: "符合時的動作：Alert / Suppress"),
                    CooldownSeconds = table.Column<int>(type: "int", nullable: true, comment: "相同告警的冷卻秒數；空值沿用全域設定"),
                    Recipients = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true, comment: "此規則的收件人（逗號分隔 Email）；空值沿用全域收件人"),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, comment: "建立者"),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "建立時間 (UTC)"),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true, comment: "最後修改者"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "最後修改時間 (UTC)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertRules", x => x.Id);
                },
                comment: "告警規則：管理者可於執行期調整的日誌告警條件。依優先序由小到大比對，第一條符合者決定結果。");

            migrationBuilder.CreateIndex(
                name: "IX_AlertRules_IsEnabled_Priority",
                schema: "core",
                table: "AlertRules",
                columns: new[] { "IsEnabled", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_AlertRules_Name",
                schema: "core",
                table: "AlertRules",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlertRules",
                schema: "core");
        }
    }
}
