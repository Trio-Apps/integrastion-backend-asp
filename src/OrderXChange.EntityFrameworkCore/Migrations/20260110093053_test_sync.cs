using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderXChange.Migrations
{
    /// <inheritdoc />
    public partial class test_sync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FoodicsAccountId",
                table: "TalabatAccounts",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<string>(
                name: "PlatformKey",
                table: "TalabatAccounts",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PlatformRestaurantId",
                table: "TalabatAccounts",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "UserName",
                table: "TalabatAccounts",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_TalabatAccounts_FoodicsAccountId",
                table: "TalabatAccounts",
                column: "FoodicsAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_TalabatAccounts_FoodicsAccounts_FoodicsAccountId",
                table: "TalabatAccounts",
                column: "FoodicsAccountId",
                principalTable: "FoodicsAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TalabatAccounts_FoodicsAccounts_FoodicsAccountId",
                table: "TalabatAccounts");

            migrationBuilder.DropIndex(
                name: "IX_TalabatAccounts_FoodicsAccountId",
                table: "TalabatAccounts");

            migrationBuilder.DropColumn(
                name: "FoodicsAccountId",
                table: "TalabatAccounts");

            migrationBuilder.DropColumn(
                name: "PlatformKey",
                table: "TalabatAccounts");

            migrationBuilder.DropColumn(
                name: "PlatformRestaurantId",
                table: "TalabatAccounts");

            migrationBuilder.DropColumn(
                name: "UserName",
                table: "TalabatAccounts");
        }
    }
}
