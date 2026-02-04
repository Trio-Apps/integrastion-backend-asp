using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderXChange.Migrations
{
    /// <inheritdoc />
    public partial class TalabatAccount_RequiredRestaurantId_UniqueVendorPerTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "TalabatAccounts",
                keyColumn: "PlatformRestaurantId",
                keyValue: null,
                column: "PlatformRestaurantId",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "PlatformRestaurantId",
                table: "TalabatAccounts",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(100)",
                oldMaxLength: 100,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_TalabatAccounts_TenantId_VendorCode",
                table: "TalabatAccounts",
                columns: new[] { "TenantId", "VendorCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TalabatAccounts_TenantId_VendorCode",
                table: "TalabatAccounts");

            migrationBuilder.AlterColumn<string>(
                name: "PlatformRestaurantId",
                table: "TalabatAccounts",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(100)",
                oldMaxLength: 100)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
