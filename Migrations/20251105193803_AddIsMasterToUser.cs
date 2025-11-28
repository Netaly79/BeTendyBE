using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeTendlyBE.Migrations
{
    /// <inheritdoc />
    public partial class AddIsMasterToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_user_role",
                table: "user");

            migrationBuilder.DropColumn(
                name: "role",
                table: "user");

            migrationBuilder.AddColumn<bool>(
                name: "is_master",
                table: "user",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_user_is_master",
                table: "user",
                column: "is_master");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_user_is_master",
                table: "user");

            migrationBuilder.DropColumn(
                name: "is_master",
                table: "user");

            migrationBuilder.AddColumn<string>(
                name: "role",
                table: "user",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_user_role",
                table: "user",
                column: "role");
        }
    }
}
