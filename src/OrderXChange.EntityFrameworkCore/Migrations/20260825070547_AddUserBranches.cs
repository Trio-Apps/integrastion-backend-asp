using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderXChange.Migrations
{
    /// <inheritdoc />
    public partial class AddUserBranches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppUserBranches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    FoodicsAccountId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    FoodicsBranchId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FoodicsBranchName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    CreationTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatorId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUserBranches", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_UserBranches_Tenant_User_Account_Branch",
                table: "AppUserBranches",
                columns: new[] { "TenantId", "UserId", "FoodicsAccountId", "FoodicsBranchId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserBranches_UserId",
                table: "AppUserBranches",
                column: "UserId");

            // Branch access moved from roles to users. Carry every existing role grant over to
            // each user holding that role, so nobody silently loses access on deploy.
            migrationBuilder.Sql(@"
                INSERT INTO AppUserBranches
                    (Id, UserId, FoodicsAccountId, FoodicsBranchId, FoodicsBranchName, TenantId, CreationTime)
                SELECT
                    UUID(), ur.UserId, rb.FoodicsAccountId, rb.FoodicsBranchId,
                    MAX(rb.FoodicsBranchName), rb.TenantId, UTC_TIMESTAMP()
                FROM AppRoleBranches rb
                JOIN AbpUserRoles ur ON ur.RoleId = rb.RoleId
                GROUP BY ur.UserId, rb.FoodicsAccountId, rb.FoodicsBranchId, rb.TenantId;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppUserBranches");
        }
    }
}
