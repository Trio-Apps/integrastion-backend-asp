using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderXChange.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeTalabatOrderLogPaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_TalabatOrderSyncLogs_Tenant_Deleted_ReceivedAt",
                table: "AppTalabatOrderSyncLogs",
                columns: new[] { "TenantId", "IsDeleted", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TalabatOrderSyncLogs_Tenant_Vendor_Status_ReceivedAt",
                table: "AppTalabatOrderSyncLogs",
                columns: new[] { "TenantId", "IsDeleted", "VendorCode", "Status", "ReceivedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TalabatOrderSyncLogs_Tenant_Deleted_ReceivedAt",
                table: "AppTalabatOrderSyncLogs");

            migrationBuilder.DropIndex(
                name: "IX_TalabatOrderSyncLogs_Tenant_Vendor_Status_ReceivedAt",
                table: "AppTalabatOrderSyncLogs");
        }
    }
}
