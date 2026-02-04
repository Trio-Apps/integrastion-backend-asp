using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderXChange.Migrations
{
    /// <inheritdoc />
    public partial class UpdateStaginTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MenuGroupId",
                table: "MenuSnapshots",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "MenuSyncRunId",
                table: "MenuSnapshots",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "AppFoodicsProductStaging",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "AppFoodicsProductStaging",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "DeletionReason",
                table: "AppFoodicsProductStaging",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "DeletionSyncError",
                table: "AppFoodicsProductStaging",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletionSyncedAt",
                table: "AppFoodicsProductStaging",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeletionSyncedToTalabat",
                table: "AppFoodicsProductStaging",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "FoodicsMenuGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    FoodicsAccountId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    BranchId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: true)
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
                    table.PrimaryKey("PK_FoodicsMenuGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FoodicsMenuGroups_FoodicsAccounts_FoodicsAccountId",
                        column: x => x.FoodicsAccountId,
                        principalTable: "FoodicsAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "MenuGroupCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    MenuGroupId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CategoryId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
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
                    table.PrimaryKey("PK_MenuGroupCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenuGroupCategories_FoodicsMenuGroups_MenuGroupId",
                        column: x => x.MenuGroupId,
                        principalTable: "FoodicsMenuGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "MenuGroupTalabatMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    FoodicsAccountId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    MenuGroupId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TalabatVendorCode = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TalabatMenuId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TalabatMenuName = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TalabatMenuDescription = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    MappingStrategy = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ConfigurationJson = table.Column<string>(type: "TEXT", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MappingEstablishedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastVerifiedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    SyncCount = table.Column<int>(type: "int", nullable: false),
                    IsTalabatValidated = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    TalabatInternalMenuId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SyncStatus = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LastSyncError = table.Column<string>(type: "TEXT", nullable: true)
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
                    table.PrimaryKey("PK_MenuGroupTalabatMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenuGroupTalabatMappings_FoodicsAccounts_FoodicsAccountId",
                        column: x => x.FoodicsAccountId,
                        principalTable: "FoodicsAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MenuGroupTalabatMappings_FoodicsMenuGroups_MenuGroupId",
                        column: x => x.MenuGroupId,
                        principalTable: "FoodicsMenuGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "MenuItemMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    FoodicsAccountId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    BranchId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MenuGroupId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    EntityType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FoodicsId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TalabatRemoteCode = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TalabatInternalId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CurrentFoodicsName = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CurrentTalabatName = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ParentMappingId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    FirstSyncedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastVerifiedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    SyncCount = table.Column<int>(type: "int", nullable: false),
                    StructureHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: true)
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
                    table.PrimaryKey("PK_MenuItemMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenuItemMappings_FoodicsAccounts_FoodicsAccountId",
                        column: x => x.FoodicsAccountId,
                        principalTable: "FoodicsAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MenuItemMappings_FoodicsMenuGroups_MenuGroupId",
                        column: x => x.MenuGroupId,
                        principalTable: "FoodicsMenuGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MenuItemMappings_MenuItemMappings_ParentMappingId",
                        column: x => x.ParentMappingId,
                        principalTable: "MenuItemMappings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "MenuSyncRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    FoodicsAccountId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    BranchId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MenuGroupId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    CorrelationId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SyncType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TriggerSource = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    InitiatedBy = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StartedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Status = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Result = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Duration = table.Column<TimeSpan>(type: "time(6)", nullable: true),
                    CurrentPhase = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProgressPercentage = table.Column<int>(type: "int", nullable: false),
                    TotalProductsProcessed = table.Column<int>(type: "int", nullable: false),
                    ProductsSucceeded = table.Column<int>(type: "int", nullable: false),
                    ProductsFailed = table.Column<int>(type: "int", nullable: false),
                    ProductsSkipped = table.Column<int>(type: "int", nullable: false),
                    ProductsAdded = table.Column<int>(type: "int", nullable: false),
                    ProductsUpdated = table.Column<int>(type: "int", nullable: false),
                    ProductsDeleted = table.Column<int>(type: "int", nullable: false),
                    CategoriesProcessed = table.Column<int>(type: "int", nullable: false),
                    ModifiersProcessed = table.Column<int>(type: "int", nullable: false),
                    TalabatVendorCode = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TalabatImportId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TalabatSubmittedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TalabatCompletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TalabatSyncStatus = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ErrorsJson = table.Column<string>(type: "TEXT", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    WarningsJson = table.Column<string>(type: "TEXT", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MetricsJson = table.Column<string>(type: "TEXT", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ConfigurationJson = table.Column<string>(type: "TEXT", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CompressedTraceData = table.Column<byte[]>(type: "LONGBLOB", nullable: true),
                    ParentSyncRunId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    CanRetry = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Tags = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
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
                    table.PrimaryKey("PK_MenuSyncRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenuSyncRuns_FoodicsAccounts_FoodicsAccountId",
                        column: x => x.FoodicsAccountId,
                        principalTable: "FoodicsAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MenuSyncRuns_FoodicsMenuGroups_MenuGroupId",
                        column: x => x.MenuGroupId,
                        principalTable: "FoodicsMenuGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MenuSyncRuns_MenuSyncRuns_ParentSyncRunId",
                        column: x => x.ParentSyncRunId,
                        principalTable: "MenuSyncRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ModifierGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    FoodicsAccountId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    BranchId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MenuGroupId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    FoodicsModifierGroupId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NameLocalized = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Version = table.Column<int>(type: "int", nullable: false),
                    MinSelection = table.Column<int>(type: "int", nullable: true),
                    MaxSelection = table.Column<int>(type: "int", nullable: true),
                    IsRequired = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    StructureHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LastUpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    IsSyncedToTalabat = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TalabatVendorCode = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: true)
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
                    table.PrimaryKey("PK_ModifierGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModifierGroups_FoodicsAccounts_FoodicsAccountId",
                        column: x => x.FoodicsAccountId,
                        principalTable: "FoodicsAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ModifierGroups_FoodicsMenuGroups_MenuGroupId",
                        column: x => x.MenuGroupId,
                        principalTable: "FoodicsMenuGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "MenuDeltas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    SourceSnapshotId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    TargetSnapshotId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    FoodicsAccountId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    BranchId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MenuGroupId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    SourceVersion = table.Column<int>(type: "int", nullable: true),
                    TargetVersion = table.Column<int>(type: "int", nullable: false),
                    DeltaType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TotalChanges = table.Column<int>(type: "int", nullable: false),
                    AddedCount = table.Column<int>(type: "int", nullable: false),
                    UpdatedCount = table.Column<int>(type: "int", nullable: false),
                    RemovedCount = table.Column<int>(type: "int", nullable: false),
                    DeltaSummaryJson = table.Column<string>(type: "TEXT", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CompressedDeltaPayload = table.Column<byte[]>(type: "LONGBLOB", nullable: true),
                    IsSyncedToTalabat = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    TalabatImportId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TalabatSyncedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TalabatVendorCode = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SyncStatus = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SyncErrorDetails = table.Column<string>(type: "TEXT", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    MenuSyncRunId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    ExtraProperties = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ConcurrencyStamp = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreationTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatorId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuDeltas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenuDeltas_FoodicsAccounts_FoodicsAccountId",
                        column: x => x.FoodicsAccountId,
                        principalTable: "FoodicsAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MenuDeltas_FoodicsMenuGroups_MenuGroupId",
                        column: x => x.MenuGroupId,
                        principalTable: "FoodicsMenuGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MenuDeltas_MenuSnapshots_SourceSnapshotId",
                        column: x => x.SourceSnapshotId,
                        principalTable: "MenuSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MenuDeltas_MenuSnapshots_TargetSnapshotId",
                        column: x => x.TargetSnapshotId,
                        principalTable: "MenuSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MenuDeltas_MenuSyncRuns_MenuSyncRunId",
                        column: x => x.MenuSyncRunId,
                        principalTable: "MenuSyncRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "MenuItemDeletion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    FoodicsAccountId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    BranchId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EntityType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EntityId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EntityName = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DeletionReason = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DeletionSource = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FoodicsDeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    EntitySnapshotJson = table.Column<string>(type: "TEXT", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AffectedEntitiesJson = table.Column<string>(type: "TEXT", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsSyncedToTalabat = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    TalabatVendorCode = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TalabatSyncedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TalabatSyncStatus = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TalabatSyncError = table.Column<string>(type: "TEXT", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TalabatSyncRetryCount = table.Column<int>(type: "int", nullable: false),
                    CanRollback = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    MenuSyncRunId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
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
                    table.PrimaryKey("PK_MenuItemDeletion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenuItemDeletion_FoodicsAccounts_FoodicsAccountId",
                        column: x => x.FoodicsAccountId,
                        principalTable: "FoodicsAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MenuItemDeletion_MenuSyncRuns_MenuSyncRunId",
                        column: x => x.MenuSyncRunId,
                        principalTable: "MenuSyncRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "MenuSyncRunSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    MenuSyncRunId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    StepType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Message = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Phase = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Timestamp = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    DataJson = table.Column<string>(type: "TEXT", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Duration = table.Column<TimeSpan>(type: "time(6)", nullable: true),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    CreationTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatorId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuSyncRunSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenuSyncRunSteps_MenuSyncRuns_MenuSyncRunId",
                        column: x => x.MenuSyncRunId,
                        principalTable: "MenuSyncRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ModifierGroupVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ModifierGroupId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NameLocalized = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MinSelection = table.Column<int>(type: "int", nullable: true),
                    MaxSelection = table.Column<int>(type: "int", nullable: true),
                    IsRequired = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    StructureHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SnapshotDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    OptionsSnapshot = table.Column<string>(type: "TEXT", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ChangeReason = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ChangedBy = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
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
                    table.PrimaryKey("PK_ModifierGroupVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModifierGroupVersions_ModifierGroups_ModifierGroupId",
                        column: x => x.ModifierGroupId,
                        principalTable: "ModifierGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ModifierOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ModifierGroupId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    FoodicsModifierOptionId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NameLocalized = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Price = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    PreviousPrice = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    PriceChangedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    ImageUrl = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LastUpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    IsSyncedToTalabat = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    PropertyHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: true)
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
                    table.PrimaryKey("PK_ModifierOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModifierOptions_ModifierGroups_ModifierGroupId",
                        column: x => x.ModifierGroupId,
                        principalTable: "ModifierGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ProductModifierAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    FoodicsAccountId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    BranchId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MenuGroupId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    FoodicsProductId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ModifierGroupId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    IsSyncedToTalabat = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TalabatVendorCode = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
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
                    table.PrimaryKey("PK_ProductModifierAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductModifierAssignments_FoodicsAccounts_FoodicsAccountId",
                        column: x => x.FoodicsAccountId,
                        principalTable: "FoodicsAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductModifierAssignments_FoodicsMenuGroups_MenuGroupId",
                        column: x => x.MenuGroupId,
                        principalTable: "FoodicsMenuGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProductModifierAssignments_ModifierGroups_ModifierGroupId",
                        column: x => x.ModifierGroupId,
                        principalTable: "ModifierGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ModifierOptionPriceHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ModifierOptionId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    OldPrice = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    NewPrice = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Reason = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ChangePercentage = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    ChangeAmount = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ChangeType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ChangeSource = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ChangedBy = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsSyncedToTalabat = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SyncedToTalabatAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
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
                    table.PrimaryKey("PK_ModifierOptionPriceHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModifierOptionPriceHistory_ModifierOptions_ModifierOptionId",
                        column: x => x.ModifierOptionId,
                        principalTable: "ModifierOptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ModifierOptionVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ModifierOptionId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NameLocalized = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Price = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    ImageUrl = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PropertyHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SnapshotDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ChangeReason = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ChangedBy = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
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
                    table.PrimaryKey("PK_ModifierOptionVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModifierOptionVersions_ModifierOptions_ModifierOptionId",
                        column: x => x.ModifierOptionId,
                        principalTable: "ModifierOptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_MenuSnapshots_Account_Branch_Group_Version",
                table: "MenuSnapshots",
                columns: new[] { "FoodicsAccountId", "BranchId", "MenuGroupId", "Version" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuSnapshots_MenuGroupId",
                table: "MenuSnapshots",
                column: "MenuGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuSnapshots_SyncRunId",
                table: "MenuSnapshots",
                column: "MenuSyncRunId");

            migrationBuilder.CreateIndex(
                name: "IX_FoodicsMenuGroups_Account_Active",
                table: "FoodicsMenuGroups",
                columns: new[] { "FoodicsAccountId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_FoodicsMenuGroups_Account_Branch",
                table: "FoodicsMenuGroups",
                columns: new[] { "FoodicsAccountId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_FoodicsMenuGroups_Account_Branch_Active",
                table: "FoodicsMenuGroups",
                columns: new[] { "FoodicsAccountId", "BranchId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_FoodicsMenuGroups_Account_Branch_Name_Tenant",
                table: "FoodicsMenuGroups",
                columns: new[] { "FoodicsAccountId", "BranchId", "Name", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FoodicsMenuGroups_FoodicsAccountId",
                table: "FoodicsMenuGroups",
                column: "FoodicsAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FoodicsMenuGroups_IsActive",
                table: "FoodicsMenuGroups",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_FoodicsMenuGroups_SortOrder",
                table: "FoodicsMenuGroups",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_FoodicsMenuGroups_TenantId",
                table: "FoodicsMenuGroups",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuDeltas_Account_Branch",
                table: "MenuDeltas",
                columns: new[] { "FoodicsAccountId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuDeltas_Account_Branch_Group",
                table: "MenuDeltas",
                columns: new[] { "FoodicsAccountId", "BranchId", "MenuGroupId" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuDeltas_Account_Status",
                table: "MenuDeltas",
                columns: new[] { "FoodicsAccountId", "SyncStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuDeltas_CreationTime",
                table: "MenuDeltas",
                column: "CreationTime");

            migrationBuilder.CreateIndex(
                name: "IX_MenuDeltas_DeltaType",
                table: "MenuDeltas",
                column: "DeltaType");

            migrationBuilder.CreateIndex(
                name: "IX_MenuDeltas_FoodicsAccountId",
                table: "MenuDeltas",
                column: "FoodicsAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuDeltas_MenuGroupId",
                table: "MenuDeltas",
                column: "MenuGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuDeltas_SourceSnapshotId",
                table: "MenuDeltas",
                column: "SourceSnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuDeltas_Status_CreationTime",
                table: "MenuDeltas",
                columns: new[] { "SyncStatus", "CreationTime" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuDeltas_SyncRunId",
                table: "MenuDeltas",
                column: "MenuSyncRunId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuDeltas_SyncStatus",
                table: "MenuDeltas",
                column: "SyncStatus");

            migrationBuilder.CreateIndex(
                name: "IX_MenuDeltas_TalabatImportId",
                table: "MenuDeltas",
                column: "TalabatImportId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuDeltas_TalabatVendorCode",
                table: "MenuDeltas",
                column: "TalabatVendorCode");

            migrationBuilder.CreateIndex(
                name: "IX_MenuDeltas_TargetSnapshotId",
                table: "MenuDeltas",
                column: "TargetSnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuGroupCategories_AssignedAt",
                table: "MenuGroupCategories",
                column: "AssignedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MenuGroupCategories_Category_Active",
                table: "MenuGroupCategories",
                columns: new[] { "CategoryId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuGroupCategories_CategoryId",
                table: "MenuGroupCategories",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuGroupCategories_Group_Active",
                table: "MenuGroupCategories",
                columns: new[] { "MenuGroupId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuGroupCategories_Group_Category_Tenant",
                table: "MenuGroupCategories",
                columns: new[] { "MenuGroupId", "CategoryId", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuGroupCategories_Group_SortOrder",
                table: "MenuGroupCategories",
                columns: new[] { "MenuGroupId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuGroupCategories_IsActive",
                table: "MenuGroupCategories",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_MenuGroupCategories_MenuGroupId",
                table: "MenuGroupCategories",
                column: "MenuGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuGroupCategories_TenantId",
                table: "MenuGroupCategories",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuGroupTalabatMappings_Account_Active",
                table: "MenuGroupTalabatMappings",
                columns: new[] { "FoodicsAccountId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuGroupTalabatMappings_Account_Vendor_MenuId",
                table: "MenuGroupTalabatMappings",
                columns: new[] { "FoodicsAccountId", "TalabatVendorCode", "TalabatMenuId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuGroupTalabatMappings_Active_Priority",
                table: "MenuGroupTalabatMappings",
                columns: new[] { "IsActive", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuGroupTalabatMappings_FoodicsAccountId",
                table: "MenuGroupTalabatMappings",
                column: "FoodicsAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuGroupTalabatMappings_IsActive",
                table: "MenuGroupTalabatMappings",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_MenuGroupTalabatMappings_IsTalabatValidated",
                table: "MenuGroupTalabatMappings",
                column: "IsTalabatValidated");

            migrationBuilder.CreateIndex(
                name: "IX_MenuGroupTalabatMappings_LastVerifiedAt",
                table: "MenuGroupTalabatMappings",
                column: "LastVerifiedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MenuGroupTalabatMappings_MappingEstablishedAt",
                table: "MenuGroupTalabatMappings",
                column: "MappingEstablishedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MenuGroupTalabatMappings_MappingStrategy",
                table: "MenuGroupTalabatMappings",
                column: "MappingStrategy");

            migrationBuilder.CreateIndex(
                name: "IX_MenuGroupTalabatMappings_MenuGroupId",
                table: "MenuGroupTalabatMappings",
                column: "MenuGroupId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuGroupTalabatMappings_Priority",
                table: "MenuGroupTalabatMappings",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_MenuGroupTalabatMappings_Status_LastVerified",
                table: "MenuGroupTalabatMappings",
                columns: new[] { "SyncStatus", "LastVerifiedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuGroupTalabatMappings_SyncStatus",
                table: "MenuGroupTalabatMappings",
                column: "SyncStatus");

            migrationBuilder.CreateIndex(
                name: "IX_MenuGroupTalabatMappings_TalabatVendorCode",
                table: "MenuGroupTalabatMappings",
                column: "TalabatVendorCode");

            migrationBuilder.CreateIndex(
                name: "IX_MenuGroupTalabatMappings_TenantId",
                table: "MenuGroupTalabatMappings",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuGroupTalabatMappings_Vendor_Active",
                table: "MenuGroupTalabatMappings",
                columns: new[] { "TalabatVendorCode", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemDeletion_FoodicsAccountId",
                table: "MenuItemDeletion",
                column: "FoodicsAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemDeletions_SyncRunId",
                table: "MenuItemDeletion",
                column: "MenuSyncRunId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemMappings_Account_Active",
                table: "MenuItemMappings",
                columns: new[] { "FoodicsAccountId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemMappings_Account_Branch",
                table: "MenuItemMappings",
                columns: new[] { "FoodicsAccountId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemMappings_Account_Branch_Entity_FoodicsId",
                table: "MenuItemMappings",
                columns: new[] { "FoodicsAccountId", "BranchId", "EntityType", "FoodicsId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemMappings_Account_Branch_Group_Entity_FoodicsId",
                table: "MenuItemMappings",
                columns: new[] { "FoodicsAccountId", "BranchId", "MenuGroupId", "EntityType", "FoodicsId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemMappings_Account_Branch_Group_TalabatCode",
                table: "MenuItemMappings",
                columns: new[] { "FoodicsAccountId", "BranchId", "MenuGroupId", "TalabatRemoteCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemMappings_Account_Branch_TalabatCode",
                table: "MenuItemMappings",
                columns: new[] { "FoodicsAccountId", "BranchId", "TalabatRemoteCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemMappings_Active_EntityType",
                table: "MenuItemMappings",
                columns: new[] { "IsActive", "EntityType" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemMappings_EntityType",
                table: "MenuItemMappings",
                column: "EntityType");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemMappings_FirstSyncedAt",
                table: "MenuItemMappings",
                column: "FirstSyncedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemMappings_FoodicsAccountId",
                table: "MenuItemMappings",
                column: "FoodicsAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemMappings_FoodicsId",
                table: "MenuItemMappings",
                column: "FoodicsId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemMappings_IsActive",
                table: "MenuItemMappings",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemMappings_LastVerifiedAt",
                table: "MenuItemMappings",
                column: "LastVerifiedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemMappings_MenuGroupId",
                table: "MenuItemMappings",
                column: "MenuGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemMappings_ParentMappingId",
                table: "MenuItemMappings",
                column: "ParentMappingId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemMappings_TalabatInternalId",
                table: "MenuItemMappings",
                column: "TalabatInternalId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemMappings_TalabatRemoteCode",
                table: "MenuItemMappings",
                column: "TalabatRemoteCode");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemMappings_TenantId",
                table: "MenuItemMappings",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuSyncRuns_Account_Branch",
                table: "MenuSyncRuns",
                columns: new[] { "FoodicsAccountId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuSyncRuns_Account_Branch_Group",
                table: "MenuSyncRuns",
                columns: new[] { "FoodicsAccountId", "BranchId", "MenuGroupId" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuSyncRuns_CorrelationId",
                table: "MenuSyncRuns",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuSyncRuns_FoodicsAccountId",
                table: "MenuSyncRuns",
                column: "FoodicsAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuSyncRuns_MenuGroupId",
                table: "MenuSyncRuns",
                column: "MenuGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuSyncRuns_ParentId",
                table: "MenuSyncRuns",
                column: "ParentSyncRunId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuSyncRuns_Retry",
                table: "MenuSyncRuns",
                columns: new[] { "CanRetry", "RetryCount", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuSyncRuns_StartedAt",
                table: "MenuSyncRuns",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MenuSyncRuns_Status",
                table: "MenuSyncRuns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MenuSyncRuns_Status_StartedAt",
                table: "MenuSyncRuns",
                columns: new[] { "Status", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuSyncRuns_TalabatVendorCode",
                table: "MenuSyncRuns",
                column: "TalabatVendorCode");

            migrationBuilder.CreateIndex(
                name: "IX_MenuSyncRunSteps_StepType",
                table: "MenuSyncRunSteps",
                column: "StepType");

            migrationBuilder.CreateIndex(
                name: "IX_MenuSyncRunSteps_SyncRun_Sequence",
                table: "MenuSyncRunSteps",
                columns: new[] { "MenuSyncRunId", "SequenceNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuSyncRunSteps_SyncRunId",
                table: "MenuSyncRunSteps",
                column: "MenuSyncRunId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuSyncRunSteps_Timestamp",
                table: "MenuSyncRunSteps",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_ModifierGroups_Account_Branch_MenuGroup",
                table: "ModifierGroups",
                columns: new[] { "FoodicsAccountId", "BranchId", "MenuGroupId" });

            migrationBuilder.CreateIndex(
                name: "IX_ModifierGroups_Active_Synced",
                table: "ModifierGroups",
                columns: new[] { "IsActive", "IsSyncedToTalabat" });

            migrationBuilder.CreateIndex(
                name: "IX_ModifierGroups_FoodicsId",
                table: "ModifierGroups",
                column: "FoodicsModifierGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ModifierGroups_MenuGroupId",
                table: "ModifierGroups",
                column: "MenuGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ModifierGroupVersions_Date",
                table: "ModifierGroupVersions",
                column: "SnapshotDate");

            migrationBuilder.CreateIndex(
                name: "IX_ModifierGroupVersions_Group_Version",
                table: "ModifierGroupVersions",
                columns: new[] { "ModifierGroupId", "Version" });

            migrationBuilder.CreateIndex(
                name: "IX_ModifierPriceHistory_Date",
                table: "ModifierOptionPriceHistory",
                column: "ChangedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ModifierPriceHistory_Option",
                table: "ModifierOptionPriceHistory",
                column: "ModifierOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_ModifierPriceHistory_Type_Date",
                table: "ModifierOptionPriceHistory",
                columns: new[] { "ChangeType", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ModifierOptions_Active_Synced",
                table: "ModifierOptions",
                columns: new[] { "IsActive", "IsSyncedToTalabat" });

            migrationBuilder.CreateIndex(
                name: "IX_ModifierOptions_FoodicsId",
                table: "ModifierOptions",
                column: "FoodicsModifierOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_ModifierOptions_ModifierGroup",
                table: "ModifierOptions",
                column: "ModifierGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ModifierOptionVersions_Date",
                table: "ModifierOptionVersions",
                column: "SnapshotDate");

            migrationBuilder.CreateIndex(
                name: "IX_ModifierOptionVersions_Option_Version",
                table: "ModifierOptionVersions",
                columns: new[] { "ModifierOptionId", "Version" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductModifierAssignments_Active_Synced",
                table: "ProductModifierAssignments",
                columns: new[] { "IsActive", "IsSyncedToTalabat" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductModifierAssignments_Context_Product",
                table: "ProductModifierAssignments",
                columns: new[] { "FoodicsAccountId", "BranchId", "MenuGroupId", "FoodicsProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductModifierAssignments_MenuGroupId",
                table: "ProductModifierAssignments",
                column: "MenuGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductModifierAssignments_ModifierGroup",
                table: "ProductModifierAssignments",
                column: "ModifierGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductModifierAssignments_Unique",
                table: "ProductModifierAssignments",
                columns: new[] { "FoodicsAccountId", "BranchId", "MenuGroupId", "FoodicsProductId", "ModifierGroupId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MenuSnapshots_FoodicsMenuGroups_MenuGroupId",
                table: "MenuSnapshots",
                column: "MenuGroupId",
                principalTable: "FoodicsMenuGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_MenuSnapshots_MenuSyncRuns_MenuSyncRunId",
                table: "MenuSnapshots",
                column: "MenuSyncRunId",
                principalTable: "MenuSyncRuns",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MenuSnapshots_FoodicsMenuGroups_MenuGroupId",
                table: "MenuSnapshots");

            migrationBuilder.DropForeignKey(
                name: "FK_MenuSnapshots_MenuSyncRuns_MenuSyncRunId",
                table: "MenuSnapshots");

            migrationBuilder.DropTable(
                name: "MenuDeltas");

            migrationBuilder.DropTable(
                name: "MenuGroupCategories");

            migrationBuilder.DropTable(
                name: "MenuGroupTalabatMappings");

            migrationBuilder.DropTable(
                name: "MenuItemDeletion");

            migrationBuilder.DropTable(
                name: "MenuItemMappings");

            migrationBuilder.DropTable(
                name: "MenuSyncRunSteps");

            migrationBuilder.DropTable(
                name: "ModifierGroupVersions");

            migrationBuilder.DropTable(
                name: "ModifierOptionPriceHistory");

            migrationBuilder.DropTable(
                name: "ModifierOptionVersions");

            migrationBuilder.DropTable(
                name: "ProductModifierAssignments");

            migrationBuilder.DropTable(
                name: "MenuSyncRuns");

            migrationBuilder.DropTable(
                name: "ModifierOptions");

            migrationBuilder.DropTable(
                name: "ModifierGroups");

            migrationBuilder.DropTable(
                name: "FoodicsMenuGroups");

            migrationBuilder.DropIndex(
                name: "IX_MenuSnapshots_Account_Branch_Group_Version",
                table: "MenuSnapshots");

            migrationBuilder.DropIndex(
                name: "IX_MenuSnapshots_MenuGroupId",
                table: "MenuSnapshots");

            migrationBuilder.DropIndex(
                name: "IX_MenuSnapshots_SyncRunId",
                table: "MenuSnapshots");

            migrationBuilder.DropColumn(
                name: "MenuGroupId",
                table: "MenuSnapshots");

            migrationBuilder.DropColumn(
                name: "MenuSyncRunId",
                table: "MenuSnapshots");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "AppFoodicsProductStaging");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "AppFoodicsProductStaging");

            migrationBuilder.DropColumn(
                name: "DeletionReason",
                table: "AppFoodicsProductStaging");

            migrationBuilder.DropColumn(
                name: "DeletionSyncError",
                table: "AppFoodicsProductStaging");

            migrationBuilder.DropColumn(
                name: "DeletionSyncedAt",
                table: "AppFoodicsProductStaging");

            migrationBuilder.DropColumn(
                name: "IsDeletionSyncedToTalabat",
                table: "AppFoodicsProductStaging");
        }
    }
}
