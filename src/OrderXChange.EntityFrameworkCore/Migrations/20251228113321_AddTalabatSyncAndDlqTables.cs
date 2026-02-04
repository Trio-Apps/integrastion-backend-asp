using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderXChange.Migrations
{
    /// <inheritdoc />
    public partial class AddTalabatSyncAndDlqTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TalabatImportId",
                table: "AppFoodicsProductStaging",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "TalabatLastError",
                table: "AppFoodicsProductStaging",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "TalabatSubmittedAt",
                table: "AppFoodicsProductStaging",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TalabatSyncCompletedAt",
                table: "AppFoodicsProductStaging",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TalabatSyncStatus",
                table: "AppFoodicsProductStaging",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "TalabatVendorCode",
                table: "AppFoodicsProductStaging",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AppDlqMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EventType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CorrelationId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AccountId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    OriginalMessage = table.Column<string>(type: "LONGTEXT", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ErrorCode = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StackTrace = table.Column<string>(type: "LONGTEXT", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    FailureType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FirstAttemptUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastAttemptUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    IsReplayed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ReplayedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ReplayedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReplayResult = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReplayErrorMessage = table.Column<string>(type: "TEXT", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsAcknowledged = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    AcknowledgedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    AcknowledgedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Priority = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
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
                    table.PrimaryKey("PK_AppDlqMessages", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AppTalabatCatalogSyncLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    FoodicsAccountId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    VendorCode = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ChainCode = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ImportId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CorrelationId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ApiVersion = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CategoriesCount = table.Column<int>(type: "int", nullable: false),
                    ProductsCount = table.Column<int>(type: "int", nullable: false),
                    CategoriesCreated = table.Column<int>(type: "int", nullable: false),
                    CategoriesUpdated = table.Column<int>(type: "int", nullable: false),
                    ProductsCreated = table.Column<int>(type: "int", nullable: false),
                    ProductsUpdated = table.Column<int>(type: "int", nullable: false),
                    ErrorsCount = table.Column<int>(type: "int", nullable: false),
                    ErrorsJson = table.Column<string>(type: "TEXT", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ResponseMessage = table.Column<string>(type: "TEXT", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SubmittedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ProcessingDurationSeconds = table.Column<int>(type: "int", nullable: true),
                    CallbackUrl = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
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
                    table.PrimaryKey("PK_AppTalabatCatalogSyncLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppTalabatCatalogSyncLogs_FoodicsAccounts_FoodicsAccountId",
                        column: x => x.FoodicsAccountId,
                        principalTable: "FoodicsAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_FoodicsProductStaging_TalabatImportId",
                table: "AppFoodicsProductStaging",
                column: "TalabatImportId");

            migrationBuilder.CreateIndex(
                name: "IX_FoodicsProductStaging_TalabatSubmittedAt",
                table: "AppFoodicsProductStaging",
                column: "TalabatSubmittedAt");

            migrationBuilder.CreateIndex(
                name: "IX_FoodicsProductStaging_TalabatSyncStatus",
                table: "AppFoodicsProductStaging",
                column: "TalabatSyncStatus");

            migrationBuilder.CreateIndex(
                name: "IX_FoodicsProductStaging_TalabatVendorCode",
                table: "AppFoodicsProductStaging",
                column: "TalabatVendorCode");

            migrationBuilder.CreateIndex(
                name: "IX_DlqMessages_AccountId",
                table: "AppDlqMessages",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_DlqMessages_CorrelationId",
                table: "AppDlqMessages",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_DlqMessages_EventType",
                table: "AppDlqMessages",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_DlqMessages_FailureType",
                table: "AppDlqMessages",
                column: "FailureType");

            migrationBuilder.CreateIndex(
                name: "IX_DlqMessages_IsAcknowledged",
                table: "AppDlqMessages",
                column: "IsAcknowledged");

            migrationBuilder.CreateIndex(
                name: "IX_DlqMessages_IsReplayed",
                table: "AppDlqMessages",
                column: "IsReplayed");

            migrationBuilder.CreateIndex(
                name: "IX_DlqMessages_LastAttemptUtc",
                table: "AppDlqMessages",
                column: "LastAttemptUtc");

            migrationBuilder.CreateIndex(
                name: "IX_DlqMessages_Pending",
                table: "AppDlqMessages",
                columns: new[] { "IsReplayed", "IsAcknowledged", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_DlqMessages_Priority",
                table: "AppDlqMessages",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_DlqMessages_TenantId",
                table: "AppDlqMessages",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TalabatCatalogSyncLogs_CorrelationId",
                table: "AppTalabatCatalogSyncLogs",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_TalabatCatalogSyncLogs_FoodicsAccountId",
                table: "AppTalabatCatalogSyncLogs",
                column: "FoodicsAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_TalabatCatalogSyncLogs_ImportId",
                table: "AppTalabatCatalogSyncLogs",
                column: "ImportId");

            migrationBuilder.CreateIndex(
                name: "IX_TalabatCatalogSyncLogs_Status",
                table: "AppTalabatCatalogSyncLogs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TalabatCatalogSyncLogs_SubmittedAt",
                table: "AppTalabatCatalogSyncLogs",
                column: "SubmittedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TalabatCatalogSyncLogs_TenantId",
                table: "AppTalabatCatalogSyncLogs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TalabatCatalogSyncLogs_VendorCode",
                table: "AppTalabatCatalogSyncLogs",
                column: "VendorCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppDlqMessages");

            migrationBuilder.DropTable(
                name: "AppTalabatCatalogSyncLogs");

            migrationBuilder.DropIndex(
                name: "IX_FoodicsProductStaging_TalabatImportId",
                table: "AppFoodicsProductStaging");

            migrationBuilder.DropIndex(
                name: "IX_FoodicsProductStaging_TalabatSubmittedAt",
                table: "AppFoodicsProductStaging");

            migrationBuilder.DropIndex(
                name: "IX_FoodicsProductStaging_TalabatSyncStatus",
                table: "AppFoodicsProductStaging");

            migrationBuilder.DropIndex(
                name: "IX_FoodicsProductStaging_TalabatVendorCode",
                table: "AppFoodicsProductStaging");

            migrationBuilder.DropColumn(
                name: "TalabatImportId",
                table: "AppFoodicsProductStaging");

            migrationBuilder.DropColumn(
                name: "TalabatLastError",
                table: "AppFoodicsProductStaging");

            migrationBuilder.DropColumn(
                name: "TalabatSubmittedAt",
                table: "AppFoodicsProductStaging");

            migrationBuilder.DropColumn(
                name: "TalabatSyncCompletedAt",
                table: "AppFoodicsProductStaging");

            migrationBuilder.DropColumn(
                name: "TalabatSyncStatus",
                table: "AppFoodicsProductStaging");

            migrationBuilder.DropColumn(
                name: "TalabatVendorCode",
                table: "AppFoodicsProductStaging");
        }
    }
}
