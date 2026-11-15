using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGS.Modules.CAL.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CALDB_Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "cal");

            migrationBuilder.CreateTable(
                name: "Calendars",
                schema: "cal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, comment: "日曆代碼（唯一；全公司為 COMPANY）"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, comment: "日曆名稱"),
                    Type = table.Column<byte>(type: "tinyint", nullable: false),
                    Color = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false, comment: "顯示顏色（#RRGGBB）"),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true, comment: "說明"),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false, comment: "已封存（專案結束或部門解散；封存後唯讀、預設不顯示）"),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "專案日曆綁定的 PMS 專案識別碼（第二期；不建外鍵，避免跨 schema 相依）"),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Calendars", x => x.Id);
                },
                comment: "日曆主檔：全公司共用、部門、專案日曆");

            migrationBuilder.CreateTable(
                name: "Holidays",
                schema: "cal",
                columns: table => new
                {
                    HolidayDate = table.Column<DateOnly>(type: "date", nullable: false, comment: "日期（主鍵）"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, comment: "名稱（例：中秋節、補班）"),
                    IsWorkday = table.Column<bool>(type: "bit", nullable: false, comment: "false＝放假；true＝補班（週末上班）"),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Holidays", x => x.HolidayDate);
                },
                comment: "公司工作日曆例外：放假（國定假日、公司休假）與補班日");

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "cal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "識別主鍵"),
                    CorrelationId = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "關聯 ID：代表整個業務流程的追蹤編號 (由前端或 API 傳入)"),
                    CausationId = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "誘發 ID：代表觸發此事件的具體 Command 名稱或 ID"),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "事件的完整型別名稱 (包含 Namespace)，用於還原物件"),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "事件序列化後的 JSON 內容"),
                    OccurredOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "事件發生的原始時間 (UTC)"),
                    ProcessedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true, comment: "訊息被成功處理的時間。若為 NULL 且 IsDead 為 false，表示待處理；若為 1900-01-01，表示正被某個 Worker 鎖定處理中。"),
                    ScheduledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true, comment: "預計下次重試的時間 (UTC)。用於實作「指數退避」，避免失敗後立即重試造成系統壓力。"),
                    RetryCount = table.Column<int>(type: "int", nullable: false, comment: "目前已重試的次數"),
                    IsDead = table.Column<bool>(type: "bit", nullable: false, comment: "當 RetryCount 超過閾值後標記為 true，背景服務將不再主動抓取。"),
                    LastError = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "最後一次處理失敗的錯誤訊息內容，便於維運排錯")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                },
                comment: "Outbox 訊息實體，用於實作「發佈者確認」模式 (Publisher Confirmed)");

            migrationBuilder.CreateTable(
                name: "CalendarEvents",
                schema: "cal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CalendarId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, comment: "標題（建議格式：[部門/專案] 會議主題 - 主持人）"),
                    Kind = table.Column<byte>(type: "tinyint", nullable: false),
                    AllDay = table.Column<bool>(type: "bit", nullable: false, comment: "全天活動：起為當日 00:00、迄為最後一天的隔日 00:00（台灣時間）"),
                    StartAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "開始時間（含時區）"),
                    EndAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "結束時間（含時區，不含）"),
                    Location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true, comment: "地點或線上會議連結"),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true, comment: "說明（議程、會前文件連結）"),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true, comment: "分類（公司大會、教育訓練、節慶活動…）"),
                    ExternalSource = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true, comment: "外部來源（例：PMS）；由其他模組推送的活動使用，與 ExternalKey 構成冪等鍵"),
                    ExternalKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true, comment: "外部來源的活動識別（例：PMS 甘特圖任務 Id）"),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarEvents_Calendars_CalendarId",
                        column: x => x.CalendarId,
                        principalSchema: "cal",
                        principalTable: "Calendars",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "日曆活動：標題、起迄、地點與說明");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_CalendarId_StartAt",
                schema: "cal",
                table: "CalendarEvents",
                columns: new[] { "CalendarId", "StartAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_ExternalSource_ExternalKey",
                schema: "cal",
                table: "CalendarEvents",
                columns: new[] { "ExternalSource", "ExternalKey" },
                unique: true,
                filter: "[ExternalKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Calendars_Code",
                schema: "cal",
                table: "Calendars",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Calendars_Type_IsArchived",
                schema: "cal",
                table: "Calendars",
                columns: new[] { "Type", "IsArchived" });

            // 1) 全公司日曆（系統唯一一份，固定識別碼）；冪等
            migrationBuilder.Sql(
                """
                IF NOT EXISTS (SELECT 1 FROM [cal].[Calendars] WHERE [Id] = '00000000-0000-0000-0000-0000000ca110')
                    INSERT INTO [cal].[Calendars] ([Id], [Code], [Name], [Type], [Color], [Description], [IsArchived], [CreatedBy], [CreatedAtUtc])
                    VALUES ('00000000-0000-0000-0000-0000000ca110', N'COMPANY', N'公司行事曆', 0, N'#0F6CBD',
                            N'全公司共用日曆：假日、補班日、公司大會與教育訓練', 0, N'system', SYSDATETIMEOFFSET());
                """);

            // 2) 一次性把 PMS 原有的國定假日（pms.Holidays）複製到 cal.Holidays（CAL 成為假日唯一來源）。
            //    刻意的跨 schema 例外：只在 pms.Holidays 存在時執行，已存在的日期不覆蓋；動態 SQL 避免 PMS 未安裝時編譯失敗。
            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[pms].[Holidays]', N'U') IS NOT NULL
                    EXEC(N'INSERT INTO [cal].[Holidays] ([HolidayDate], [Name], [IsWorkday], [CreatedBy], [CreatedAtUtc], [UpdatedBy], [UpdatedAtUtc])
                           SELECT s.[HolidayDate], s.[Name], s.[IsWorkday], ISNULL(s.[CreatedBy], N''pms-migration''), s.[CreatedAtUtc], s.[UpdatedBy], s.[UpdatedAtUtc]
                           FROM [pms].[Holidays] s
                           WHERE NOT EXISTS (SELECT 1 FROM [cal].[Holidays] h WHERE h.[HolidayDate] = s.[HolidayDate])');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CalendarEvents",
                schema: "cal");

            migrationBuilder.DropTable(
                name: "Holidays",
                schema: "cal");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "cal");

            migrationBuilder.DropTable(
                name: "Calendars",
                schema: "cal");
        }
    }
}
