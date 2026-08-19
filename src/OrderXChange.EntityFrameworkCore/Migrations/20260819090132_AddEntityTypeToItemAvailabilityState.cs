using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderXChange.Migrations
{
    /// <inheritdoc />
    public partial class AddEntityTypeToItemAvailabilityState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ItemAvailability_Tenant_Account_Product_Vendor",
                table: "AppItemAvailabilityStates");

            migrationBuilder.AddColumn<string>(
                name: "EntityType",
                table: "AppItemAvailabilityStates",
                type: "varchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Product")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ItemAvailability_Tenant_Account_Product_Vendor",
                table: "AppItemAvailabilityStates",
                columns: new[] { "TenantId", "FoodicsAccountId", "EntityType", "FoodicsProductId", "VendorCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ItemAvailability_Tenant_Account_Product_Vendor",
                table: "AppItemAvailabilityStates");

            migrationBuilder.DropColumn(
                name: "EntityType",
                table: "AppItemAvailabilityStates");

            migrationBuilder.CreateIndex(
                name: "IX_ItemAvailability_Tenant_Account_Product_Vendor",
                table: "AppItemAvailabilityStates",
                columns: new[] { "TenantId", "FoodicsAccountId", "FoodicsProductId", "VendorCode" },
                unique: true);
        }
    }
}
