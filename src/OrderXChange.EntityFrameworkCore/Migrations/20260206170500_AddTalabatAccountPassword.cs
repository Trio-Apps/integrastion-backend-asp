using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderXChange.EntityFrameworkCore.Migrations
{
    public partial class AddTalabatAccountPassword : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Password",
                table: "TalabatAccounts",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Password",
                table: "TalabatAccounts");
        }
    }
}
