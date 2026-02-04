using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderXChange.Migrations
{
    /// <inheritdoc />
    public partial class AddedVersionMenu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MenuSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    FoodicsAccountId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    BranchId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Version = table.Column<int>(type: "int", nullable: false),
                    SnapshotHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProductsCount = table.Column<int>(type: "int", nullable: false),
                    CategoriesCount = table.Column<int>(type: "int", nullable: false),
                    ModifiersCount = table.Column<int>(type: "int", nullable: false),
                    SnapshotDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    IsSyncedToTalabat = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    TalabatImportId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TalabatSyncedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TalabatVendorCode = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ChangelogJson = table.Column<string>(type: "TEXT", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CompressedSnapshotData = table.Column<byte[]>(type: "LONGBLOB", nullable: true),
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
                    table.PrimaryKey("PK_MenuSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenuSnapshots_FoodicsAccounts_FoodicsAccountId",
                        column: x => x.FoodicsAccountId,
                        principalTable: "FoodicsAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "MenuChangeLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    MenuSnapshotId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PreviousVersion = table.Column<int>(type: "int", nullable: true),
                    CurrentVersion = table.Column<int>(type: "int", nullable: false),
                    ChangeType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EntityType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EntityId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EntityName = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OldValueJson = table.Column<string>(type: "TEXT", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NewValueJson = table.Column<string>(type: "TEXT", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ChangedFields = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    ExtraProperties = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ConcurrencyStamp = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreationTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatorId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuChangeLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenuChangeLogs_MenuSnapshots_MenuSnapshotId",
                        column: x => x.MenuSnapshotId,
                        principalTable: "MenuSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_MenuChangeLogs_ChangeType_EntityType",
                table: "MenuChangeLogs",
                columns: new[] { "ChangeType", "EntityType" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuChangeLogs_Entity",
                table: "MenuChangeLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuChangeLogs_SnapshotId",
                table: "MenuChangeLogs",
                column: "MenuSnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuChangeLogs_Versions",
                table: "MenuChangeLogs",
                columns: new[] { "CurrentVersion", "PreviousVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuSnapshots_Account_Branch_Date",
                table: "MenuSnapshots",
                columns: new[] { "FoodicsAccountId", "BranchId", "SnapshotDate" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuSnapshots_Account_Branch_Version",
                table: "MenuSnapshots",
                columns: new[] { "FoodicsAccountId", "BranchId", "Version" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuSnapshots_Hash",
                table: "MenuSnapshots",
                column: "SnapshotHash");

            migrationBuilder.CreateIndex(
                name: "IX_MenuSnapshots_Vendor_Synced",
                table: "MenuSnapshots",
                columns: new[] { "TalabatVendorCode", "IsSyncedToTalabat" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MenuChangeLogs");

            migrationBuilder.DropTable(
                name: "MenuSnapshots");
        }
    }
}
