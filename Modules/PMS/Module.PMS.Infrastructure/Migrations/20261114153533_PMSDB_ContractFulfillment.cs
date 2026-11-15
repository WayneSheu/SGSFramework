using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGS.Modules.PMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PMSDB_ContractFulfillment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContractAcceptances",
                schema: "pms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    DeliveredDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DeemedDue = table.Column<DateOnly>(type: "date", nullable: true, comment: "擬制驗收期限：交付日起算合約約定工作日；合約無擬制驗收條款時為 null"),
                    DefectReportedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DefectNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true, comment: "甲方具體瑕疵清單摘要"),
                    AcceptedDate = table.Column<DateOnly>(type: "date", nullable: true, comment: "驗收完成日（雙方簽認日或擬制驗收日）"),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LastReminderAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractAcceptances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractAcceptances_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalSchema: "pms",
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "驗收進度：交付日、擬制驗收期限、瑕疵清單與驗收完成日");

            migrationBuilder.CreateTable(
                name: "ContractAmendments",
                schema: "pms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Source = table.Column<byte>(type: "tinyint", nullable: false),
                    ChangeRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "來源 CR（自動掛帳時；同一張 CR 只掛帳一次）"),
                    CrNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true, comment: "來源 CR 單號"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AmountDelta = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, comment: "追加（正）或減少（負）金額，新台幣未稅"),
                    NewDeliveryDate = table.Column<DateOnly>(type: "date", nullable: true, comment: "變更後的新交付／驗收日（無變更為 null）"),
                    WarrantyMonthsDelta = table.Column<int>(type: "int", nullable: false, comment: "保固延長月數（可為負）"),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractAmendments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractAmendments_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalSchema: "pms",
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "合約變更：追加金額、新交付日與保固延長");

            migrationBuilder.CreateTable(
                name: "ContractCoopItems",
                schema: "pms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, comment: "協力事項"),
                    Kind = table.Column<byte>(type: "tinyint", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true, comment: "詳細說明（例：待簽認文件名稱、環境規格）"),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false, comment: "甲方應完成日（合約或專案時程約定）"),
                    CompletedDate = table.Column<DateOnly>(type: "date", nullable: true, comment: "甲方實際完成日"),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    LastReminderAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "最近一次內部提醒時間（每個台灣日最多一次）"),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractCoopItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractCoopItems_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalSchema: "pms",
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "甲方協力事項：應完成日、實際完成日與遲延追蹤");

            migrationBuilder.CreateTable(
                name: "ContractPaymentTerms",
                schema: "pms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Percent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false, comment: "占合約現行金額的比例（%）"),
                    Trigger = table.Column<byte>(type: "tinyint", nullable: false),
                    PlannedDate = table.Column<DateOnly>(type: "date", nullable: true, comment: "預計請款日"),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    InvoiceNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    InvoicedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PaidDate = table.Column<DateOnly>(type: "date", nullable: true),
                    InvoicedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true, comment: "請款時的金額（當時現行金額 × 比例），作為開立依據留存"),
                    LastReminderAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractPaymentTerms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractPaymentTerms_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalSchema: "pms",
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "付款期程：比例、觸發條件、請款與收款狀態");

            migrationBuilder.CreateTable(
                name: "ContractReviewRounds",
                schema: "pms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeliverableItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoundNo = table.Column<int>(type: "int", nullable: false, comment: "第幾次送審（同一文件）"),
                    SubmittedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ResponseDue = table.Column<DateOnly>(type: "date", nullable: true, comment: "甲方回覆期限；合約未約定期限時為 null"),
                    Outcome = table.Column<byte>(type: "tinyint", nullable: false),
                    RespondedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true, comment: "甲方意見摘要或備註"),
                    ExceedsRoundLimit = table.Column<bool>(type: "bit", nullable: false, comment: "彙整修改次數超過合約約定（超出部分須走 CR 另行計費）"),
                    LastReminderAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractReviewRounds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractReviewRounds_ContractDeliverableItems_DeliverableItemId",
                        column: x => x.DeliverableItemId,
                        principalSchema: "pms",
                        principalTable: "ContractDeliverableItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContractReviewRounds_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalSchema: "pms",
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "文件審查回合：送審日、回覆期限、結果；逾期可擬制通過");

            migrationBuilder.CreateTable(
                name: "ContractExtensionNotices",
                schema: "pms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CoopItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false, comment: "合約內流水號"),
                    DelayWorkdays = table.Column<int>(type: "int", nullable: false, comment: "寄出當下的累計遲延工作日"),
                    ExtensionWorkdays = table.Column<int>(type: "int", nullable: false, comment: "通知甲方順延的工作日（登記為工期展延）"),
                    RequestedBy = table.Column<DateOnly>(type: "date", nullable: false, comment: "請甲方提供的期限"),
                    Mode = table.Column<byte>(type: "tinyint", nullable: false),
                    SentTo = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true, comment: "收件人與副本摘要（僅 Email 模式）"),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PdfFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PdfFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    SentBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SentByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractExtensionNotices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractExtensionNotices_ContractCoopItems_CoopItemId",
                        column: x => x.CoopItemId,
                        principalSchema: "pms",
                        principalTable: "ContractCoopItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContractExtensionNotices_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalSchema: "pms",
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "協力遲延與工期展延通知書：寄送對象、方式與展延工作日");

            migrationBuilder.CreateIndex(
                name: "IX_ContractAcceptances_ContractId",
                schema: "pms",
                table: "ContractAcceptances",
                column: "ContractId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractAcceptances_Status_DeemedDue",
                schema: "pms",
                table: "ContractAcceptances",
                columns: new[] { "Status", "DeemedDue" });

            migrationBuilder.CreateIndex(
                name: "IX_ContractAmendments_ChangeRequestId",
                schema: "pms",
                table: "ContractAmendments",
                column: "ChangeRequestId",
                unique: true,
                filter: "[ChangeRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ContractAmendments_ContractId_EffectiveDate",
                schema: "pms",
                table: "ContractAmendments",
                columns: new[] { "ContractId", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ContractCoopItems_ContractId_IsDeleted_Status_DueDate",
                schema: "pms",
                table: "ContractCoopItems",
                columns: new[] { "ContractId", "IsDeleted", "Status", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ContractExtensionNotices_ContractId_Number",
                schema: "pms",
                table: "ContractExtensionNotices",
                columns: new[] { "ContractId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractExtensionNotices_CoopItemId",
                schema: "pms",
                table: "ContractExtensionNotices",
                column: "CoopItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractPaymentTerms_ContractId_SortOrder",
                schema: "pms",
                table: "ContractPaymentTerms",
                columns: new[] { "ContractId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ContractPaymentTerms_Status_PlannedDate",
                schema: "pms",
                table: "ContractPaymentTerms",
                columns: new[] { "Status", "PlannedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ContractReviewRounds_ContractId_Outcome_ResponseDue",
                schema: "pms",
                table: "ContractReviewRounds",
                columns: new[] { "ContractId", "Outcome", "ResponseDue" });

            migrationBuilder.CreateIndex(
                name: "IX_ContractReviewRounds_DeliverableItemId_RoundNo",
                schema: "pms",
                table: "ContractReviewRounds",
                columns: new[] { "DeliverableItemId", "RoundNo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContractAcceptances",
                schema: "pms");

            migrationBuilder.DropTable(
                name: "ContractAmendments",
                schema: "pms");

            migrationBuilder.DropTable(
                name: "ContractExtensionNotices",
                schema: "pms");

            migrationBuilder.DropTable(
                name: "ContractPaymentTerms",
                schema: "pms");

            migrationBuilder.DropTable(
                name: "ContractReviewRounds",
                schema: "pms");

            migrationBuilder.DropTable(
                name: "ContractCoopItems",
                schema: "pms");
        }
    }
}
