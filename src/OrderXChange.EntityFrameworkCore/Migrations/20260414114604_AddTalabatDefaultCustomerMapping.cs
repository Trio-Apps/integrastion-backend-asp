using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderXChange.Migrations
{
    /// <inheritdoc />
    public partial class AddTalabatDefaultCustomerMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefaultFoodicsCustomerAddressId",
                table: "TalabatAccounts",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "DefaultFoodicsCustomerAddressName",
                table: "TalabatAccounts",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "DefaultFoodicsCustomerId",
                table: "TalabatAccounts",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "DefaultFoodicsCustomerName",
                table: "TalabatAccounts",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultFoodicsCustomerAddressId",
                table: "TalabatAccounts");

            migrationBuilder.DropColumn(
                name: "DefaultFoodicsCustomerAddressName",
                table: "TalabatAccounts");

            migrationBuilder.DropColumn(
                name: "DefaultFoodicsCustomerId",
                table: "TalabatAccounts");

            migrationBuilder.DropColumn(
                name: "DefaultFoodicsCustomerName",
                table: "TalabatAccounts");
        }
    }
}
