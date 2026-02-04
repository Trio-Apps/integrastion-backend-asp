using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderXChange.Migrations
{
    /// <inheritdoc />
    public partial class FoodicsAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_FoodicsProductStaging_Account_Product",
                table: "AppFoodicsProductStaging",
                columns: new[] { "FoodicsAccountId", "FoodicsProductId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FoodicsProductStaging_Account_Product",
                table: "AppFoodicsProductStaging");
        }
    }
}
