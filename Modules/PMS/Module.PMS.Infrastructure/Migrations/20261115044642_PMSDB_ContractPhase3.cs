using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGS.Modules.PMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PMSDB_ContractPhase3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DueHours",
                schema: "pms",
                table: "ContractSlaLevels",
                type: "decimal(6,2)",
                precision: 6,
                scale: 2,
                nullable: true,
                comment: "處理期限（時鐘小時，例：Level 1 於 4 小時內提出替代方案）；與 DueWorkdays 擇一，皆空＝不覆寫系統 SLA");

            migrationBuilder.AddColumn<int>(
                name: "DueWorkdays",
                schema: "pms",
                table: "ContractSlaLevels",
                type: "int",
                nullable: true,
                comment: "處理期限（工作日，例：Level 2 於 2 個工作日內提供修正）；與 DueHours 擇一");

            migrationBuilder.AddColumn<int>(
                name: "WarrantyReminderStage",
                schema: "pms",
                table: "ContractAcceptances",
                type: "int",
                nullable: false,
                defaultValue: 0,
                comment: "保固到期提醒進度：0 未提醒、1 已提醒 60 天、2 已提醒 30 天、3 已提醒 7 天、4 已提醒到期");

            migrationBuilder.CreateTable(
                name: "ContractComplianceItems",
                schema: "pms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, comment: "項目代碼（例 FIN_DR、MED_SAMD、MFG_DOWNTIME）"),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true, comment: "說明：納入的條款位置，或不適用的原因"),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractComplianceItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractComplianceItems_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalSchema: "pms",
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "產業合規清單：金融／醫療／製造專屬條款的處理狀態");

            migrationBuilder.CreateTable(
                name: "ContractSupportRequests",
                schema: "pms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false, comment: "合約內流水號"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, comment: "支援事由"),
                    WindowStart = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "預計支援起（含時區）"),
                    WindowEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "預計支援迄（含時區）"),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    EstimatedHours = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false, comment: "預估總時數（含標準服務時間內）"),
                    EstimatedOvertimeHours = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false, comment: "預估加班時數（標準服務時間以外）"),
                    EstimatedWeightedHours = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false, comment: "預估加權時數（加班時數 × 倍率合計）"),
                    ActualStart = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ActualEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ActualOvertimeHours = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: true, comment: "實際加班時數"),
                    ActualWeightedHours = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: true, comment: "實際加權時數（請款依據）"),
                    RequestedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DecidedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecidedByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true, comment: "核准／駁回意見、完成或取消備註"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractSupportRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractSupportRequests_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalSchema: "pms",
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "非服務時間支援申請：窗口、加班加權時數、核准與實際執行");

            migrationBuilder.CreateIndex(
                name: "IX_ContractComplianceItems_ContractId_Code",
                schema: "pms",
                table: "ContractComplianceItems",
                columns: new[] { "ContractId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractSupportRequests_ContractId_Number",
                schema: "pms",
                table: "ContractSupportRequests",
                columns: new[] { "ContractId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractSupportRequests_ContractId_Status",
                schema: "pms",
                table: "ContractSupportRequests",
                columns: new[] { "ContractId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContractComplianceItems",
                schema: "pms");

            migrationBuilder.DropTable(
                name: "ContractSupportRequests",
                schema: "pms");

            migrationBuilder.DropColumn(
                name: "DueHours",
                schema: "pms",
                table: "ContractSlaLevels");

            migrationBuilder.DropColumn(
                name: "DueWorkdays",
                schema: "pms",
                table: "ContractSlaLevels");

            migrationBuilder.DropColumn(
                name: "WarrantyReminderStage",
                schema: "pms",
                table: "ContractAcceptances");
        }
    }
}
