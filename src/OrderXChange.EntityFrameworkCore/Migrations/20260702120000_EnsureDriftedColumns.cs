using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderXChange.Migrations
{
    /// <summary>
    /// Idempotently ensures the schema objects from two earlier migrations that shipped
    /// WITHOUT their .Designer.cs files (AddFoodicsTalabatOrderTagId, AddTalabatCatalogPayloadHash).
    /// EF only recognizes a migration by the [Migration] attribute in its Designer, so those two
    /// were silently skipped and their columns/index never got created on fresh databases —
    /// causing "Unknown column 'TalabatOrderTagId'". The model snapshot already contains them,
    /// so this corrective migration re-applies them via IF NOT EXISTS: safe on databases that
    /// already have the objects (patched manually), and self-healing on any fresh database.
    /// </summary>
    public partial class EnsureDriftedColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE `FoodicsAccounts` ADD COLUMN IF NOT EXISTS `TalabatOrderTagId` varchar(64) CHARACTER SET utf8mb4 NULL;");
            migrationBuilder.Sql("ALTER TABLE `AppTalabatCatalogSyncLogs` ADD COLUMN IF NOT EXISTS `CatalogPayloadHash` varchar(100) CHARACTER SET utf8mb4 NULL;");
            migrationBuilder.Sql("ALTER TABLE `AppTalabatCatalogSyncLogs` ADD COLUMN IF NOT EXISTS `CatalogPayloadHashVersion` varchar(50) CHARACTER SET utf8mb4 NULL;");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS `IX_TalabatCatalogSyncLogs_PayloadHash` ON `AppTalabatCatalogSyncLogs` (`FoodicsAccountId`, `VendorCode`, `ChainCode`, `CatalogPayloadHash`);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS `IX_TalabatCatalogSyncLogs_PayloadHash` ON `AppTalabatCatalogSyncLogs`;");
            migrationBuilder.Sql("ALTER TABLE `AppTalabatCatalogSyncLogs` DROP COLUMN IF EXISTS `CatalogPayloadHashVersion`;");
            migrationBuilder.Sql("ALTER TABLE `AppTalabatCatalogSyncLogs` DROP COLUMN IF EXISTS `CatalogPayloadHash`;");
            migrationBuilder.Sql("ALTER TABLE `FoodicsAccounts` DROP COLUMN IF EXISTS `TalabatOrderTagId`;");
        }
    }
}
