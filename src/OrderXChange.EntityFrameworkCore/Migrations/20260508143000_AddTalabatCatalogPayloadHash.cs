using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderXChange.Migrations
{
    /// <inheritdoc />
    public partial class AddTalabatCatalogPayloadHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CatalogPayloadHash",
                table: "AppTalabatCatalogSyncLogs",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "CatalogPayloadHashVersion",
                table: "AppTalabatCatalogSyncLogs",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_TalabatCatalogSyncLogs_PayloadHash",
                table: "AppTalabatCatalogSyncLogs",
                columns: new[] { "FoodicsAccountId", "VendorCode", "ChainCode", "CatalogPayloadHash" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TalabatCatalogSyncLogs_PayloadHash",
                table: "AppTalabatCatalogSyncLogs");

            migrationBuilder.DropColumn(
                name: "CatalogPayloadHash",
                table: "AppTalabatCatalogSyncLogs");

            migrationBuilder.DropColumn(
                name: "CatalogPayloadHashVersion",
                table: "AppTalabatCatalogSyncLogs");
        }
    }
}
