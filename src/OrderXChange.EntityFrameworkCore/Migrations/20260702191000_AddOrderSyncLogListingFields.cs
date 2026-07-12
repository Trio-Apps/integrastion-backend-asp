using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderXChange.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderSyncLogListingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Channel",
                table: "AppTalabatOrderSyncLogs",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "CustomerAddress",
                table: "AppTalabatOrderSyncLogs",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "CustomerId",
                table: "AppTalabatOrderSyncLogs",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "CustomerName",
                table: "AppTalabatOrderSyncLogs",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountTotal",
                table: "AppTalabatOrderSyncLogs",
                type: "decimal(65,30)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExpeditionType",
                table: "AppTalabatOrderSyncLogs",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "GrandTotal",
                table: "AppTalabatOrderSyncLogs",
                type: "decimal(65,30)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "AppTalabatOrderSyncLogs",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Channel",
                table: "AppTalabatOrderSyncLogs");

            migrationBuilder.DropColumn(
                name: "CustomerAddress",
                table: "AppTalabatOrderSyncLogs");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "AppTalabatOrderSyncLogs");

            migrationBuilder.DropColumn(
                name: "CustomerName",
                table: "AppTalabatOrderSyncLogs");

            migrationBuilder.DropColumn(
                name: "DiscountTotal",
                table: "AppTalabatOrderSyncLogs");

            migrationBuilder.DropColumn(
                name: "ExpeditionType",
                table: "AppTalabatOrderSyncLogs");

            migrationBuilder.DropColumn(
                name: "GrandTotal",
                table: "AppTalabatOrderSyncLogs");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "AppTalabatOrderSyncLogs");
        }
    }
}
