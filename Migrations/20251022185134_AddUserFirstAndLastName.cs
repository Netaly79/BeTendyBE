using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeTendlyBE.Migrations
{
    /// <inheritdoc />
    public partial class AddUserFirstAndLastName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "first_name",
                table: "user",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "last_name",
                table: "user",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "first_name",
                table: "user");

            migrationBuilder.DropColumn(
                name: "last_name",
                table: "user");
        }
    }
}
