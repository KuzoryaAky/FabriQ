using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ImageProcessor.Migrations
{
    /// <inheritdoc />
    public partial class AddStatusFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "ProcessedDate",
                table: "MeasurementRecords",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "MeasurementRecords",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "MeasurementRecords",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "ErrorMessage",
                table: "MeasurementRecords",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RequestGuid",
                table: "MeasurementRecords",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "ResultJson",
                table: "MeasurementRecords",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "MeasurementRecords",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "MeasurementRecords");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "MeasurementRecords");

            migrationBuilder.DropColumn(
                name: "ErrorMessage",
                table: "MeasurementRecords");

            migrationBuilder.DropColumn(
                name: "RequestGuid",
                table: "MeasurementRecords");

            migrationBuilder.DropColumn(
                name: "ResultJson",
                table: "MeasurementRecords");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "MeasurementRecords");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ProcessedDate",
                table: "MeasurementRecords",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }
    }
}
