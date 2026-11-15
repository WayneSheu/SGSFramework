using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGS.Modules.PMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PMSDB_ContractSupportClient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientRequestChannel",
                schema: "pms",
                table: "ContractSupportRequests",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                comment: "甲方提出方式（Email／電話／會議／即時通訊／其他）");

            migrationBuilder.AddColumn<string>(
                name: "ClientRequestRef",
                schema: "pms",
                table: "ContractSupportRequests",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                comment: "甲方提出的佐證說明（信件主旨、聯絡人、單號等）");

            migrationBuilder.AddColumn<DateOnly>(
                name: "ClientRequestedDate",
                schema: "pms",
                table: "ContractSupportRequests",
                type: "date",
                nullable: true,
                comment: "甲方提出此支援需求的日期（佐證：甲方須提前申請）");

            migrationBuilder.AddColumn<bool>(
                name: "SupportRequiresApproval",
                schema: "pms",
                table: "Contracts",
                type: "bit",
                nullable: false,
                defaultValue: true,
                comment: "非服務時間支援是否須乙方高階主管內部核准（預設是；否則申請即視為已核准）");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClientRequestChannel",
                schema: "pms",
                table: "ContractSupportRequests");

            migrationBuilder.DropColumn(
                name: "ClientRequestRef",
                schema: "pms",
                table: "ContractSupportRequests");

            migrationBuilder.DropColumn(
                name: "ClientRequestedDate",
                schema: "pms",
                table: "ContractSupportRequests");

            migrationBuilder.DropColumn(
                name: "SupportRequiresApproval",
                schema: "pms",
                table: "Contracts");
        }
    }
}
