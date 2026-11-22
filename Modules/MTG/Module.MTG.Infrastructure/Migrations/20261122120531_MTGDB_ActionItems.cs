using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGS.Modules.MTG.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MTGDB_ActionItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActionItems",
                schema: "mtg",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "識別主鍵"),
                    MeetingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "所屬會議"),
                    Seq = table.Column<int>(type: "int", nullable: false, comment: "會議內顯示順序（從 1 起）"),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false, comment: "待辦內容"),
                    Owner = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true, comment: "負責人姓名（文字；紀要未提及時為空）"),
                    OwnerEmail = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true, comment: "負責人 Email（個資，落地加密；通知郵件的收件人）"),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true, comment: "期限日期（能從紀要文字解析成日期時才有）"),
                    DueText = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true, comment: "紀要中的期限描述（例如「第三周」「下週五前」），無法解析成日期時保留供人工判讀"),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, comment: "Open、InProgress、Done、Cancelled"),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true, comment: "備註"),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, comment: "Extracted（由紀要抽取）、Manual（人工新增）"),
                    MinutesVersion = table.Column<int>(type: "int", nullable: true, comment: "抽取自哪一版紀要（人工新增為空）"),
                    NotifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "最近一次寄出通知郵件的時間（UTC）；未通知為空"),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, comment: "建立人員"),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "建立時間"),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true, comment: "最後異動人員"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "最後異動時間；有值代表曾被人工修改（重新抽取時不會覆蓋）")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActionItems_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalSchema: "mtg",
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "會議待辦事項：由紀要自動抽取或人工新增，可指派負責人與期限，並以郵件通知");

            migrationBuilder.CreateIndex(
                name: "IX_ActionItems_DueDate",
                schema: "mtg",
                table: "ActionItems",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_ActionItems_MeetingId_Seq",
                schema: "mtg",
                table: "ActionItems",
                columns: new[] { "MeetingId", "Seq" });

            migrationBuilder.CreateIndex(
                name: "IX_ActionItems_Status",
                schema: "mtg",
                table: "ActionItems",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActionItems",
                schema: "mtg");
        }
    }
}
