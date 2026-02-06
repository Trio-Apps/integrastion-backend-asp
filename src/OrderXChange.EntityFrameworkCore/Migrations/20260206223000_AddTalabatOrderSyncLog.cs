using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderXChange.Migrations
{
    /// <inheritdoc />
    public partial class AddTalabatOrderSyncLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppTalabatOrderSyncLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    FoodicsAccountId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    VendorCode = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PlatformRestaurantId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OrderToken = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OrderCode = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ShortCode = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CorrelationId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsTestOrder = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ProductsCount = table.Column<int>(type: "int", nullable: false),
                    CategoriesCount = table.Column<int>(type: "int", nullable: false),
                    OrderCreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    LastAttemptUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ErrorCode = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FoodicsOrderId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FoodicsResponseJson = table.Column<string>(type: "LONGTEXT", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    WebhookPayloadJson = table.Column<string>(type: "LONGTEXT", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    ExtraProperties = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ConcurrencyStamp = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreationTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatorId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    LastModificationTime = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    DeletionTime = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppTalabatOrderSyncLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppTalabatOrderSyncLogs_FoodicsAccounts_FoodicsAccountId",
                        column: x => x.FoodicsAccountId,
                        principalTable: "FoodicsAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_TalabatOrderSyncLogs_CorrelationId",
                table: "AppTalabatOrderSyncLogs",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_TalabatOrderSyncLogs_FoodicsAccountId",
                table: "AppTalabatOrderSyncLogs",
                column: "FoodicsAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_TalabatOrderSyncLogs_OrderCode",
                table: "AppTalabatOrderSyncLogs",
                column: "OrderCode");

            migrationBuilder.CreateIndex(
                name: "IX_TalabatOrderSyncLogs_OrderToken",
                table: "AppTalabatOrderSyncLogs",
                column: "OrderToken");

            migrationBuilder.CreateIndex(
                name: "IX_TalabatOrderSyncLogs_ReceivedAt",
                table: "AppTalabatOrderSyncLogs",
                column: "ReceivedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TalabatOrderSyncLogs_Status",
                table: "AppTalabatOrderSyncLogs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TalabatOrderSyncLogs_TenantId",
                table: "AppTalabatOrderSyncLogs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TalabatOrderSyncLogs_VendorCode",
                table: "AppTalabatOrderSyncLogs",
                column: "VendorCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppTalabatOrderSyncLogs");
        }
    }
}
