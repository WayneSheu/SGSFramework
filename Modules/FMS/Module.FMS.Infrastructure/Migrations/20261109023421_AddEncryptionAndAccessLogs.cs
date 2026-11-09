using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGS.Modules.FMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEncryptionAndAccessLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EncryptionVersion",
                schema: "fms",
                table: "FileMetadatas",
                type: "int",
                nullable: true,
                comment: "儲存加密格式版本（1 = SGSE 分塊 AES-256-GCM）；null 表示未加密（舊資料或公開分類）");

            migrationBuilder.AddColumn<long>(
                name: "StoredSize",
                schema: "fms",
                table: "FileMetadatas",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                comment: "儲存體上的實際大小（位元組，加密時為密文大小）");

            migrationBuilder.AddColumn<string>(
                name: "WrappedKey",
                schema: "fms",
                table: "FileMetadatas",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true,
                comment: "以 Data Protection 主金鑰環包裝的檔案資料金鑰（fk:v1:）；明文金鑰不落地");

            // 既有檔案皆為明文儲存：儲存大小即明文大小
            migrationBuilder.Sql("UPDATE [fms].[FileMetadatas] SET [StoredSize] = [FileSize] WHERE [StoredSize] = 0;");

            // 存取稽核為 SQL Server Ledger 僅附加資料表：任何人（含 DBA）皆無法 UPDATE / DELETE（零信任稽核軌跡）
            migrationBuilder.Sql(@"
                CREATE TABLE [fms].[FileAccessLogs] (
                    [Id] uniqueidentifier NOT NULL,
                    [FileId] uniqueidentifier NULL,
                    [Category] nvarchar(100) NULL,
                    [Action] nvarchar(30) NOT NULL,
                    [IsSuccess] bit NOT NULL,
                    [UserId] nvarchar(64) NULL,
                    [UserName] nvarchar(256) NOT NULL,
                    [IsSystemAccount] bit NOT NULL,
                    [ClientId] nvarchar(128) NULL,
                    [RemoteIp] nvarchar(64) NULL,
                    [ForwardedFor] nvarchar(256) NULL,
                    [UserAgent] nvarchar(512) NULL,
                    [TraceId] nvarchar(64) NULL,
                    [Detail] nvarchar(500) NULL,
                    [OccurredAt] datetimeoffset NOT NULL,
                    CONSTRAINT [PK_FileAccessLogs] PRIMARY KEY ([Id])
                ) WITH (LEDGER = ON (APPEND_ONLY = ON));
            ");

            migrationBuilder.CreateIndex(
                name: "IX_FileAccessLogs_FileId_OccurredAt",
                schema: "fms",
                table: "FileAccessLogs",
                columns: new[] { "FileId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FileAccessLogs_OccurredAt",
                schema: "fms",
                table: "FileAccessLogs",
                column: "OccurredAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FileAccessLogs",
                schema: "fms");

            migrationBuilder.DropColumn(
                name: "EncryptionVersion",
                schema: "fms",
                table: "FileMetadatas");

            migrationBuilder.DropColumn(
                name: "StoredSize",
                schema: "fms",
                table: "FileMetadatas");

            migrationBuilder.DropColumn(
                name: "WrappedKey",
                schema: "fms",
                table: "FileMetadatas");
        }
    }
}
