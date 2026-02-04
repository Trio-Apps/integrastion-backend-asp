using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderXChange.Migrations
{
    /// <inheritdoc />
    public partial class AddIdempotencyRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppIdempotencyRecords",
                columns: table => new
                {
                    AccountId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    IdempotencyKey = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FirstSeenUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastProcessedUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ResultHash = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExpiresAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppIdempotencyRecords", x => new { x.AccountId, x.IdempotencyKey });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_AccountId",
                table: "AppIdempotencyRecords",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_ExpiresAt",
                table: "AppIdempotencyRecords",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_LastProcessedUtc",
                table: "AppIdempotencyRecords",
                column: "LastProcessedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_Status",
                table: "AppIdempotencyRecords",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppIdempotencyRecords");
        }
    }
}
