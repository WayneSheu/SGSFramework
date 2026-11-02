using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class report_category_permissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ItemType",
                schema: "core",
                table: "AccessReviewItems",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "RoleMembership",
                comment: "覆核項目類型（RoleMembership=角色指派, ReportCategoryGrant=報告類別授權, SignerQualification=簽署人資格）");

            migrationBuilder.AddColumn<Guid>(
                name: "SubjectId",
                schema: "core",
                table: "AccessReviewItems",
                type: "uniqueidentifier",
                nullable: true,
                comment: "非角色指派項目所對應之報告類別授權或簽署人資格識別鍵（回收時據以撤銷）");

            migrationBuilder.CreateTable(
                name: "ReportCategories",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "識別主鍵"),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, comment: "類別代碼（大寫英數，唯一）"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, comment: "類別名稱"),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true, comment: "說明"),
                    DefaultConfidentiality = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, comment: "新建報告預設密級（Normal=常規, Confidential=機密, Restricted=特檢）"),
                    RegulationTags = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true, comment: "適用法規 / 認證標籤（逗號分隔，例：TFDA,ISO17025）"),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false, comment: "排序"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, comment: "是否啟用"),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "建立時間"),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, comment: "建立人員"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "最後更新時間"),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true, comment: "最後更新人員"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false, comment: "並行版本（樂觀鎖）")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportCategories", x => x.Id);
                },
                comment: "報告類別主檔（檢驗領域，例：食品 FOOD、電子 RoHS、材料 MAT、環境 ENV），為報告權限隔離的核心維度");

            migrationBuilder.CreateTable(
                name: "ReportCategoryGrants",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "識別主鍵"),
                    PrincipalType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, comment: "授權對象類型（User=使用者, Role=角色）"),
                    PrincipalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "授權對象識別鍵（使用者或角色 Id）"),
                    CategoryCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, comment: "類別代碼（ReportCategory.Code）"),
                    TenantLabId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "限定區域實驗室；null 表示不限實驗室"),
                    Actions = table.Column<int>(type: "int", nullable: false, comment: "授權動作位元旗標（View=1, Edit=2, Review=4, Sign=8, Void=16, Download=32, ViewAudit=64, ViewBlindIdentity=128）"),
                    DataScope = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, comment: "資料範圍（Assigned=僅指派, Lab=所屬實驗室, Global=全域）"),
                    MaxConfidentiality = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, comment: "可存取的最高密級（Normal=常規, Confidential=機密, Restricted=特檢）"),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "生效起始（含）"),
                    EffectiveTo = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "生效迄止（含）；null 表示無期限"),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true, comment: "授權原因"),
                    RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "撤銷時間"),
                    RevokedByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true, comment: "撤銷人員"),
                    RevokeReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true, comment: "撤銷原因"),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "建立時間"),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, comment: "建立人員"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "最後更新時間"),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true, comment: "最後更新人員")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportCategoryGrants", x => x.Id);
                },
                comment: "報告類別授權：授予使用者或角色於「類別 × 區域實驗室」下可執行的動作、資料範圍與可存取的最高密級。紀錄不刪除，以撤銷保留稽核軌跡");

            migrationBuilder.CreateTable(
                name: "ReportCategoryLabAssignments",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "識別主鍵"),
                    CategoryCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, comment: "類別代碼（ReportCategory.Code）"),
                    TenantLabId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "區域實驗室（ORG Organization.TenantLabId）"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "建立時間"),
                    CreatedByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true, comment: "建立人員")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportCategoryLabAssignments", x => x.Id);
                },
                comment: "報告類別承作實驗室：指定哪些區域實驗室（分公司）可承作此類別報告");

            migrationBuilder.CreateTable(
                name: "ReportGrantChangeRequests",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "識別主鍵"),
                    RequestType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, comment: "申請類型（CreateGrant=新增類別授權, CreateQualification=新增簽署人資格）"),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "申請內容（新增授權或資格之完整請求 JSON），核准時據以套用"),
                    Summary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false, comment: "申請摘要（顯示用）"),
                    SubjectUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "受授權之使用者（授權對象為使用者或簽署人資格時），核准者不得為此人"),
                    RequestReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true, comment: "申請原因"),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, comment: "申請狀態（Pending=待審核, Approved=核准, Rejected=駁回, Cancelled=撤回）"),
                    RequestedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "申請人識別鍵"),
                    RequestedByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, comment: "申請人名稱"),
                    RequestedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "申請時間"),
                    ReviewedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "審核人識別鍵"),
                    ReviewedByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true, comment: "審核人名稱"),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "審核時間"),
                    ReviewComment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true, comment: "審核意見"),
                    AppliedEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "核准後建立之授權或資格識別鍵"),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "建立時間"),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, comment: "建立人員"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "最後更新時間"),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true, comment: "最後更新人員")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportGrantChangeRequests", x => x.Id);
                },
                comment: "報告授權變更申請（Maker-Checker）：敏感的類別授權與所有簽署人資格須經第二人核准方生效");

            migrationBuilder.CreateTable(
                name: "SignerQualifications",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "識別主鍵"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "使用者識別鍵"),
                    CategoryCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, comment: "類別代碼（ReportCategory.Code）"),
                    TenantLabId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "區域實驗室（ORG Organization.TenantLabId）"),
                    CertificateNo = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, comment: "認證機構核發之簽署人編號（例：TAF 報告簽署人編號）"),
                    ValidFrom = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "資格生效起始（含）"),
                    ValidTo = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "資格生效迄止（含）"),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true, comment: "備註"),
                    RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "撤銷時間"),
                    RevokedByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true, comment: "撤銷人員"),
                    RevokeReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true, comment: "撤銷原因"),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "建立時間"),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, comment: "建立人員"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "最後更新時間"),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true, comment: "最後更新人員")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignerQualifications", x => x.Id);
                },
                comment: "報告簽署人資格：使用者於「類別 × 區域實驗室」之認可簽署資格與有效期間。紀錄不刪除，以撤銷保留稽核軌跡");

            migrationBuilder.CreateIndex(
                name: "IX_ReportCategories_Code",
                schema: "core",
                table: "ReportCategories",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportCategoryGrants_CategoryCode_TenantLabId",
                schema: "core",
                table: "ReportCategoryGrants",
                columns: new[] { "CategoryCode", "TenantLabId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportCategoryGrants_PrincipalType_PrincipalId",
                schema: "core",
                table: "ReportCategoryGrants",
                columns: new[] { "PrincipalType", "PrincipalId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportCategoryLabAssignments_CategoryCode_TenantLabId",
                schema: "core",
                table: "ReportCategoryLabAssignments",
                columns: new[] { "CategoryCode", "TenantLabId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportCategoryLabAssignments_TenantLabId",
                schema: "core",
                table: "ReportCategoryLabAssignments",
                column: "TenantLabId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportGrantChangeRequests_Status_RequestedAt",
                schema: "core",
                table: "ReportGrantChangeRequests",
                columns: new[] { "Status", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SignerQualifications_UserId_CategoryCode_TenantLabId",
                schema: "core",
                table: "SignerQualifications",
                columns: new[] { "UserId", "CategoryCode", "TenantLabId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReportCategories",
                schema: "core");

            migrationBuilder.DropTable(
                name: "ReportCategoryGrants",
                schema: "core");

            migrationBuilder.DropTable(
                name: "ReportCategoryLabAssignments",
                schema: "core");

            migrationBuilder.DropTable(
                name: "ReportGrantChangeRequests",
                schema: "core");

            migrationBuilder.DropTable(
                name: "SignerQualifications",
                schema: "core");

            migrationBuilder.DropColumn(
                name: "ItemType",
                schema: "core",
                table: "AccessReviewItems");

            migrationBuilder.DropColumn(
                name: "SubjectId",
                schema: "core",
                table: "AccessReviewItems");
        }
    }
}
